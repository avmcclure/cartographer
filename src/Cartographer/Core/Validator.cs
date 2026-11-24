using System.Text.RegularExpressions;

namespace Cartographer;

public sealed record Issue(string Id, bool Error, string Message)
{
    public override string ToString() => $"{(Error ? "error" : "warning")}: {Id}: {Message}";
}

/// <summary>Checks every entry against spec section 3. Errors make an atlas invalid; warnings do not.</summary>
public static class Validator
{
    static readonly Regex Secret = new(@"(?i)\b(password|pwd|secret|token|accountkey|sharedaccesskey|api[_-]?key)\s*=\s*[^\s;""'<>{}\[\]]{3,}|-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.Compiled);
    static readonly Regex Pointer = new(@"^(person:[^@]+@\d{4}-\d{2}-\d{2}|instance:[^@]+@\d{4}-\d{2}-\d{2}|[a-z0-9._-]+:[^@]+@[0-9a-f]{7,40})$", RegexOptions.Compiled);
    static readonly Regex Lines = new(@"#L\d+(-L\d+)?$", RegexOptions.Compiled);
    static readonly Regex Segment = new(@"^[a-z0-9._-]+$", RegexOptions.Compiled);
    static readonly Regex Date = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    public static List<Issue> Check(Atlas atlas)
    {
        var issues = new List<Issue>();
        foreach (var (id, reason) in atlas.Unreadable) issues.Add(new(id, true, reason));
        foreach (var e in atlas.Entries.Values) Check(atlas, e, issues);
        return issues;
    }

    static void Check(Atlas atlas, Entry e, List<Issue> issues)
    {
        void Error(string m) => issues.Add(new(e.Id, true, m));
        void Warn(string m) => issues.Add(new(e.Id, false, m));
        var limits = atlas.Config.Limits;

        if (!e.Id.Split('/').All(Segment.IsMatch)) Error("the path has characters outside a-z, 0-9, '.', '_' and '-'");
        if (!Kinds.Paths.TryGetValue(e.Kind, out var pattern)) { Error($"kind '{e.Kind}' is not known"); return; }
        if (!Regex.IsMatch(e.Id, pattern)) Error($"a {e.Kind} cannot live at this path");
        if (string.IsNullOrWhiteSpace(e.Name)) Error("name is required");
        if (string.IsNullOrWhiteSpace(e.Summary)) Error("summary is required");
        else if (e.Summary.Contains('\n') || e.Summary.Length > 200) Error("summary must be one line of at most 200 characters");
        if (e.Type is not null && (!Kinds.Types.TryGetValue(e.Kind, out var types) || !types.Contains(e.Type)))
            Error(Kinds.Types.TryGetValue(e.Kind, out types) ? $"type '{e.Type}' is not one of {string.Join(", ", types)}" : $"a {e.Kind} has no type");
        if (e.Kind == Kinds.Component && e.Type is null) Error("a component needs a type");
        if (e.Status is not null and not "retired") Error($"status '{e.Status}' is not known; only 'retired' is");

        var area = Ids.Area(e.Id);
        if (e.Kind == Kinds.Component && e.Repository != area + "/repository") Error($"repository must be '{area}/repository'");
        if (e.Kind is Kinds.Table or Kinds.View or Kinds.Routine or Kinds.Trigger && e.Database != area + "/database") Error($"database must be '{area}/database'");
        if (e.Kind != Kinds.Component && (e.Repository is not null || e.Provides is not null || e.Packages is not null)) Error("repository, provides and packages belong on components only");
        if (e.Kind != Kinds.Repository && e.Locations is not null) Error("locations belong on repositories only");
        if (e.Kind != Kinds.Database && (e.Instances is not null || e.Aliases is not null || e.Engine is not null)) Error("engine, aliases and instances belong on databases only");
        foreach (var l in e.Locations ?? [])
            if (!Regex.IsMatch(l.Id, @"^(github:[^/]+/[^/]+|azdo:[^/]+/[^/]+/.+)$")) Error($"location '{l.Id}' must be github:<org>/<repo> or azdo:<org>/<project>/<repo>");
        if (e.Locations?.Count(l => l.Primary) > 1) Error("only one location can be primary");

        foreach (var (key, pointer) in e.Sources ?? [])
        {
            if (!Regex.IsMatch(key, "^[a-z0-9-]+$")) Error($"source key '{key}' must be lower-case letters, digits and hyphens");
            if (!Pointer.IsMatch(pointer)) Error($"source '{key}' is not a valid evidence pointer: {pointer}");
        }
        void Fact(string what, string basis, string? source)
        {
            if (!Basis.All.Contains(basis)) Error($"{what}: basis '{basis}' is not one of extracted, inferred, stated");
            if (string.IsNullOrWhiteSpace(source)) { Error($"{what}: a source is required"); return; }
            var bare = Lines.Replace(source, "");
            if (e.Sources?.ContainsKey(bare) != true && !Pointer.IsMatch(bare)) Error($"{what}: source '{source}' is neither a key in sources nor a full evidence pointer");
        }
        foreach (var p in e.Provides ?? [])
        {
            Fact($"provides {p.Name}", p.Basis, p.Source);
            if (p.As is not ("nuget" or "assembly" or "progid" or "typelib" or "namespace" or "contract" or "package")) Error($"provides {p.Name}: 'as' must be nuget, assembly, progid, typelib, namespace, contract or package");
        }
        foreach (var s in e.Systems ?? [])
        {
            Fact($"system {s.Id}", s.Basis, s.Source);
            if (atlas.Find(s.Id)?.Kind != Kinds.System) Warn($"system '{s.Id}' has no entry");
        }
        foreach (var t in e.Tags ?? []) Fact($"tag {t.Name}", t.Basis, t.Source);
        if (e.Owner is not null) Fact("owner", e.Owner.Basis, e.Owner.Source);
        if (e.Role is not null)
        {
            Fact("role", e.Role.Basis, e.Role.Source);
            if (!LinkTypes.Roles.Contains(e.Role.Name)) Error($"role '{e.Role.Name}' is not one of {string.Join(", ", LinkTypes.Roles)}");
            if (e.Kind is not (Kinds.Component or Kinds.External)) Error("role belongs on components and external systems only");
        }
        if (e.Systems is not null && e.Kind is not (Kinds.Component or Kinds.Database)) Error("systems belong on components and databases only");
        if (e.Tags is not null && e.Kind is not (Kinds.Table or Kinds.View)) Error("tags belong on tables and views only");

        var seen = new HashSet<string>();
        foreach (var l in e.Links ?? [])
        {
            var what = $"link {l.Type} {l.To ?? l.Name}";
            Fact(what, l.Basis, l.Source);
            if (!LinkTypes.Rules.TryGetValue(l.Type, out var rule)) { Error($"{what}: link type is not known"); continue; }
            if (!rule.From.Contains(e.Kind)) Error($"{what}: a {e.Kind} cannot have this link");
            if (l.To is null && string.IsNullOrWhiteSpace(l.Name)) Error($"{what}: needs 'to', or a 'name' when unresolved");
            if (l.To is not null)
            {
                var target = atlas.Find(l.To);
                if (target is null) Warn($"{what}: the target has no entry");
                else if (!rule.To.Contains(target.Kind)) Error($"{what}: cannot point at a {target.Kind}");
            }
            if (l.How is not null && !LinkTypes.Transports.Contains(l.How)) Error($"{what}: how '{l.How}' is not one of {string.Join(", ", LinkTypes.Transports)}");
            if (l.Via is not null && atlas.Find(l.Via) is null) Warn($"{what}: via '{l.Via}' has no entry");
            if (!seen.Add(l.Identity)) Error($"{what}: the same link appears twice");
        }

        if (e.Verified is not null)
        {
            if (!Date.IsMatch(e.Verified.Date)) Error("verified.date must be YYYY-MM-DD");
            else if ((DateTime.Parse(Clock.Today) - DateTime.Parse(e.Verified.Date)).TotalDays > limits.VerifiedAgeDays) Warn($"last verified on {e.Verified.Date}, more than {limits.VerifiedAgeDays} days ago");
        }
        foreach (var i in e.Instances ?? [])
        {
            if (i.Read is not null && !Date.IsMatch(i.Read)) Error($"instance {i.Name}: read must be YYYY-MM-DD");
            if (i.Permission is not (null or "full" or "definitions" or "limited")) Error($"instance {i.Name}: permission must be full, definitions or limited");
        }

        var unknown = Sections.Split(e.Body).Select(s => s.Title).Where(t => t != "" && !Sections.Order.Contains(t)).ToList();
        if (unknown.Count > 0) Error($"unknown section(s): {string.Join(", ", unknown)}. Allowed: {string.Join(", ", Sections.Order)}");
        var questions = QuestionList.Parse(e.Body);
        foreach (var p in questions.Problems) Error(p);
        if (questions.Items.Count > limits.EntryQuestions) Warn($"{questions.Items.Count} open questions; more than {limits.EntryQuestions} is too many to work through");

        var text = EntryFile.Write(e);
        var secret = Secret.Match(text);
        if (secret.Success) Error($"looks like it contains a credential ('{secret.Value[..Math.Min(12, secret.Value.Length)]}…'); record where it is, never its value");
        var lineCount = text.Count(c => c == '\n');
        if (lineCount > limits.EntryLines) Warn($"{lineCount} lines; the limit is {limits.EntryLines}. Describe and point at the source instead of copying it");
    }
}
