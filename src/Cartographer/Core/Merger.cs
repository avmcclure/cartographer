using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>Where a fact came from: a repository folder name, "instance:&lt;name&gt;", or "person".</summary>
public static class Evidence
{
    static readonly Regex Lines = new(@"#L\d+(-L\d+)?$");

    public static string Bare(string source) => Lines.Replace(source, "");

    public static string? Pointer(Entry e, string? source)
    {
        if (source is null) return null;
        var bare = Bare(source);
        return e.Sources?.GetValueOrDefault(bare) ?? bare;
    }

    public static string? Origin(Entry e, string? source) => OriginOf(Pointer(e, source));

    public static string? OriginOf(string? pointer)
    {
        if (pointer is null) return null;
        if (pointer.StartsWith("instance:")) return pointer.Split('@')[0];
        if (pointer.StartsWith("person:")) return "person";
        var colon = pointer.IndexOf(':');
        return colon > 0 ? pointer[..colon] : null;
    }
}

/// <summary>Merges what a mechanical pass (or a live read) produced into the atlas (spec section 6, "Merge").</summary>
public static class Merger
{
    /// <param name="origin">The repository folder name, or "instance:&lt;name&gt;".</param>
    /// <param name="previouslyOwned">Entries this origin produced last time; those not produced now lose its facts and are retired if nothing else supports them.</param>
    public static void Apply(Atlas atlas, string origin, IEnumerable<Entry> produced, IEnumerable<string>? previouslyOwned = null)
    {
        var producedIds = new HashSet<string>();
        foreach (var p in produced)
        {
            producedIds.Add(p.Id);
            if (!atlas.Entries.TryGetValue(p.Id, out var e))
            {
                p.Body = Sections.SetExtracted(p.Body, p.Extracted);
                Tidy(p);
                atlas.Entries[p.Id] = p;
                continue;
            }
            // What an agent added to an extracted link (the library types used and how often) survives the link being re-extracted.
            var usage = (e.Links ?? []).Where(l => l.Uses is not null || l.Count is not null).GroupBy(l => l.Identity).ToDictionary(g => g.Key, g => (g.First().Uses, g.First().Count));
            DropFacts(e, origin);
            if (origin.StartsWith("instance:")) RecordDrift(e, p, origin);
            // Rename the incoming source keys that clash with keys kept from another origin.
            var rename = new Dictionary<string, string>();
            var taken = new HashSet<string>();
            e.Sources ??= new(StringComparer.Ordinal);
            foreach (var (key, pointer) in p.Sources ?? [])
            {
                // Free means: unused, or held by this same origin (an older commit of the same file), and not claimed earlier in this pass.
                bool Free(string k) => !taken.Contains(k) && (!e.Sources.TryGetValue(k, out var kept) || kept == pointer || Evidence.OriginOf(kept) == origin);
                var final = key;
                for (var n = 2; !Free(final); n++) final = $"{key}-{n}";
                e.Sources[final] = pointer;
                taken.Add(final);
                if (final != key) rename[key] = final;
            }
            string? Rekey(string? source)
            {
                if (source is null) return null;
                var bare = Evidence.Bare(source);
                return rename.TryGetValue(bare, out var k) ? k + source[bare.Length..] : source;
            }
            foreach (var x in p.Provides ?? []) { x.Source = Rekey(x.Source); if (!(e.Provides ??= []).Any(o => o.Name == x.Name && o.As == x.As)) e.Provides.Add(x); }
            foreach (var l in p.Links ?? [])
            {
                l.Source = Rekey(l.Source);
                e.Links ??= [];
                // An extracted link replaces the same link an agent inferred; a stated one stays.
                e.Links.RemoveAll(o => o.Identity == l.Identity && o.Basis == Basis.Inferred);
                if (usage.TryGetValue(l.Identity, out var kept)) { l.Uses ??= kept.Uses; l.Count ??= kept.Count; }
                if (!e.Links.Any(o => o.Identity == l.Identity)) e.Links.Add(l);
            }
            foreach (var d in p.Drift ?? []) (e.Drift ??= []).Add(d);
            if (p.Stack is not null) e.Stack = (e.Stack ?? []).Union(p.Stack).ToList();
            if (p.Locations is not null) e.Locations = p.Locations;
            if (p.Packages is not null) e.Packages = p.Packages;
            if (p.Engine is not null) e.Engine = p.Engine;
            if (p.Instances is not null) e.Instances = (e.Instances ?? []).Where(i => p.Instances.All(n => n.Name != i.Name)).Concat(p.Instances).OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
            if (p.Verified is not null && !origin.StartsWith("instance:")) e.Verified = p.Verified;
            e.Verified ??= p.Verified;
            e.Status = null;
            if (!origin.StartsWith("instance:") || p.Extracted is not null && !Sections.Get(e.Body, Sections.Technical).Contains("<!-- extracted -->"))
                e.Body = Sections.SetExtracted(e.Body, p.Extracted);
            Tidy(e);
        }
        foreach (var id in previouslyOwned ?? [])
        {
            if (producedIds.Contains(id) || !atlas.Entries.TryGetValue(id, out var e)) continue;
            DropFacts(e, origin);
            if (!origin.StartsWith("instance:") && (e.Sources is null || e.Sources.Values.All(v => Evidence.OriginOf(v) is var o && (o is "person" or null || o == origin))))
                e.Status = "retired";
            Tidy(e);
        }
    }

    /// <summary>
    /// A live read that disagrees with what a repository says about the same table or view is drift (spec section 3).
    /// ponytail: columns only, and only repository against live. Two repositories defining one table differently are
    /// not compared (the later scan wins); compare per-origin column sets if that turns out to matter.
    /// </summary>
    static void RecordDrift(Entry e, Entry live, string origin)
    {
        var repositories = (e.Sources ?? []).Values.Select(Evidence.OriginOf).Where(o => o is not null && o != "person" && !o.StartsWith("instance:")).Distinct().Order(StringComparer.Ordinal).ToList();
        var inRepo = DbText.ParseColumns(e.Body);
        var onLive = DbText.ParseColumns(live.Extracted ?? "");
        if (repositories.Count == 0 || inRepo.Count == 0 || onLive.Count == 0) return;
        var instance = origin["instance:".Length..];
        var differences = new List<string>();
        foreach (var c in inRepo)
        {
            var other = onLive.FirstOrDefault(o => o.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase));
            if (other is null) differences.Add($"column {c.Name} is in the repository but not on {instance}");
            else if (other.Type != c.Type || other.Nullable != c.Nullable)
                differences.Add($"column {c.Name} is {Describe(c)} in the repository and {Describe(other)} on {instance}");
        }
        foreach (var c in onLive.Where(o => !inRepo.Any(r => r.Name.Equals(o.Name, StringComparison.OrdinalIgnoreCase))))
            differences.Add($"column {c.Name} is on {instance} but not in the repository");
        if (differences.Count > 0) (e.Drift ??= []).Add(new Drift { What = string.Join("; ", differences), Sources = [.. repositories!, origin] });
        static string Describe(Column c) => $"{c.Type}{(c.Nullable ? " null" : " not null")}";
    }

    /// <summary>Removes the extracted facts that came from one origin, and its sources that nothing else cites.</summary>
    static void DropFacts(Entry e, string origin)
    {
        bool Ours(string basis, string? source) => basis == Basis.Extracted && Evidence.Origin(e, source) == origin;
        e.Provides?.RemoveAll(p => Ours(p.Basis, p.Source));
        e.Links?.RemoveAll(l => Ours(l.Basis, l.Source));
        e.Drift?.RemoveAll(d => d.Sources.Contains(origin));
        if (e.Sources is null) return;
        var cited = new[] { e.Owner?.Source }.Concat((e.Provides ?? []).Select(p => p.Source)).Concat((e.Links ?? []).Select(l => l.Source))
            .Concat((e.Systems ?? []).Select(s => s.Source)).Concat((e.Tags ?? []).Select(t => t.Source))
            .Where(s => s is not null).Select(s => Evidence.Bare(s!)).ToHashSet();
        foreach (var key in e.Sources.Where(s => Evidence.OriginOf(s.Value) == origin && !cited.Contains(s.Key)).Select(s => s.Key).ToList()) e.Sources.Remove(key);
    }

    /// <summary>Stable order, so the same facts always give the same file.</summary>
    public static void Tidy(Entry e)
    {
        e.Links = e.Links?.OrderBy(l => Array.IndexOf(LinkTypes.Rules.Keys.ToArray(), l.Type)).ThenBy(l => l.To ?? "~" + l.Name, StringComparer.Ordinal).ThenBy(l => l.Via, StringComparer.Ordinal).ToList();
        e.Provides = e.Provides?.OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.As, StringComparer.Ordinal).ToList();
        e.Packages = e.Packages?.Distinct().Order(StringComparer.OrdinalIgnoreCase).ToList();
        e.Stack = e.Stack?.Distinct().ToList();
    }
}

/// <summary>Atlas-wide pass after a merge: resolves names to entries and raises or withdraws the CLI's own questions (spec section 6, "Resolver").</summary>
public static class Resolver
{
    const string ProviderPrefix = "Which component provides ";
    const string BrokenPrefix = "What replaces the link ";

    public static void Run(Atlas atlas)
    {
        var provided = atlas.OfKind(Kinds.Component).Where(c => c.Status is null)
            .SelectMany(c => (c.Provides ?? []).Select(p => (p.Name, p.As, c.Id))).ToList();
        // Which components provide a name, counting only the kinds of name this link can mean.
        List<string> Providers(Entry from, Link l)
        {
            string[] kinds = l.Type == "calls" ? [l.How == "com" ? "progid" : "contract"] : ["nuget", "assembly", "typelib"];
            static string Bare(string n) => Regex.Replace(n, @"\.(dll|exe|ocx|tlb)$", "", RegexOptions.IgnoreCase);
            var candidates = provided.Where(p => kinds.Contains(p.As) && p.Id != from.Id).ToList();
            var hits = candidates.Where(p => Bare(p.Name).Equals(Bare(l.Name!), StringComparison.OrdinalIgnoreCase)).ToList();
            // A generated WCF client often names the contract "<ReferenceName>.IOrderService": fall back to the interface's own name.
            if (hits.Count == 0 && l.How == "wcf") hits = candidates.Where(p => p.Name.Split('.')[^1].Equals(l.Name!.Split('.')[^1], StringComparison.OrdinalIgnoreCase)).ToList();
            return hits.Select(p => p.Id).Distinct().Order(StringComparer.Ordinal).ToList();
        }
        var packageProviders = provided.Where(p => p.As is "nuget" or "assembly").GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);
        var prefixes = atlas.Config.InternalPrefixes.Select(p => new Regex("^" + Regex.Escape(p).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase)).ToList();
        var databases = atlas.OfKind(Kinds.Database).ToList();

        foreach (var e in atlas.Entries.Values.ToList())
        {
            var wanted = new List<(string Missing, string WaitingOn, string Looks)>();

            // Public packages that have since gained a provider become links again.
            foreach (var package in (e.Packages ?? []).ToList())
            {
                var name = package.Split(' ')[0];
                if (!packageProviders.TryGetValue(name, out var ids) || ids.All(id => id == e.Id)) continue;
                e.Packages!.Remove(package);
                Extraction.AddLink(e, new Link { Type = "depends-on", Name = name, Version = package.Contains(' ') ? package[(name.Length + 1)..] : null, Basis = Basis.Extracted, Source = e.Sources?.Keys.FirstOrDefault() ?? "unknown" });
            }

            foreach (var l in (e.Links ?? []).ToList())
            {
                // A COM or WCF call names a provided name just as a depends-on does, but it is never a public package.
                if ((l.Type == "depends-on" || l is { Type: "calls", How: "com" or "wcf" }) && l.To is null && l.Name is not null)
                {
                    var ids = Providers(e, l);
                    if (ids.Count == 1)
                    {
                        if (e.Links!.Any(o => o != l && o.Type == l.Type && o.To == ids[0])) e.Links!.Remove(l); else l.To = ids[0];
                    }
                    else if (ids.Count > 1)
                        wanted.Add(($"{ProviderPrefix}'{l.Name}' for this component; {ids.Count} components provide that name: {string.Join(", ", ids)}.", $"name {l.Name}", "One of the listed components, set as the link's 'to'."));
                    else if (prefixes.Any(p => p.IsMatch(l.Name)))
                        wanted.Add(($"{ProviderPrefix}'{l.Name}'. It matches an internal name prefix but no scanned repository provides it.", $"name {l.Name}", $"A component whose provides list contains {l.Name}, or a person stating it is public."));
                    else if (l.Type == "calls" || Regex.IsMatch(l.Name, @"^\{|\.(dll|exe|ocx|tlb)$", RegexOptions.IgnoreCase))
                        // A file, type library or run-time contract is never a public package: it stays an unresolved reference.
                        wanted.Add(($"{ProviderPrefix}'{l.Name}'. No scanned repository provides it.", $"name {l.Name}", $"A component whose provides list contains {l.Name}, or a person stating it comes from outside the organisation."));
                    else if (l.Basis == Basis.Extracted && l.Type == "depends-on")
                    {
                        e.Links!.Remove(l);
                        (e.Packages ??= []).Add(l.Version is null ? l.Name : $"{l.Name} {l.Version}");
                    }
                }
                else if (l is { Type: "uses", To: null, Name: not null })
                {
                    var dbs = databases.Where(d => d.Status is null && (d.Name.Equals(l.Name, StringComparison.OrdinalIgnoreCase) || d.Id == Ids.Database(l.Name) || (d.Aliases ?? []).Contains(l.Name, StringComparer.OrdinalIgnoreCase))).ToList();
                    if (dbs.Count == 1)
                    {
                        if (e.Links!.Any(o => o != l && o.Type == "uses" && o.To == dbs[0].Id)) e.Links!.Remove(l); else l.To = dbs[0].Id;
                    }
                }
                else if (l.To is null && l.Name is not null && l.Type is "calls" or "reads" or "writes")
                {
                    var match = MatchObject(atlas, databases, l);
                    if (match is not null && !e.Links!.Any(o => o != l && o.Type == l.Type && o.To == match && o.Via == l.Via)) { l.To = match; l.Database = null; }
                    else if (match is not null) e.Links!.Remove(l);
                }
                if (l.To is not null && atlas.Find(l.To) is not { Status: null })
                    wanted.Add(($"{BrokenPrefix}'{l.Type} {l.To}'. Its target {(atlas.Find(l.To) is null ? "no longer has an entry" : "is retired")}.", $"entry {l.To}", "The entry this link should now point at, or confirmation that the link is gone."));
            }

            Merger.Tidy(e);
            if (e.Packages?.Count == 0) e.Packages = null;
            var existing = QuestionList.Parse(e.Body);
            // A dismissed note that names the thing means a person or agent has said not to ask again.
            wanted.RemoveAll(w => existing.Dismissed.Any(d => d.Contains(w.WaitingOn[(w.WaitingOn.IndexOf(' ') + 1)..], StringComparison.OrdinalIgnoreCase)));
            var stale = existing.Items.Where(q => q.RaisedBy == "cli" && q.State == "open" && (q.Missing.StartsWith(ProviderPrefix) || q.Missing.StartsWith(BrokenPrefix)) && wanted.All(w => !SameQuestion(w.Missing, q))).ToList();
            var missing = wanted.Where(w => existing.Items.All(q => !SameQuestion(w.Missing, q))).ToList();
            if (stale.Count == 0 && missing.Count == 0) continue;
            QuestionList.Update(e, list =>
            {
                list.Items.RemoveAll(q => stale.Any(s => s.Id == q.Id));
                foreach (var w in missing) list.Add(w.Missing, w.WaitingOn, w.Looks, "cli");
            });
        }
    }

    static bool SameQuestion(string missing, Question q) => new Question { Missing = missing }.Key == q.Key;

    /// <summary>Finds the one routine or table a name like "dbo.sp_x", "sp_x" or "Sales.dbo.Orders" refers to, or null.</summary>
    static string? MatchObject(Atlas atlas, List<Entry> databases, Link l)
    {
        var parts = l.Name!.Replace("[", "").Replace("]", "").Replace("\"", "").Split('.', StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 4) return null;
        var name = parts[^1];
        var schema = parts.Length >= 2 && parts[^2].Length > 0 ? parts[^2] : null;
        var hint = l.Database ?? (parts.Length >= 3 ? parts[^3] : null);
        var kinds = l.Type == "calls" ? new[] { Kinds.Routine } : [Kinds.Table, Kinds.View];
        var candidates = atlas.OfKind(kinds).Where(o => o.Status is null && o.Name.Split('.')[^1].Equals(name, StringComparison.OrdinalIgnoreCase)
            && (schema is null || o.Id.Split('/')[3].Equals(Ids.Slug(schema), StringComparison.OrdinalIgnoreCase))).ToList();
        if (hint is not null)
        {
            var dbs = databases.Where(d => d.Name.Equals(hint, StringComparison.OrdinalIgnoreCase) || d.Id == Ids.Database(hint) || (d.Aliases ?? []).Contains(hint, StringComparer.OrdinalIgnoreCase)).Select(d => d.Id).ToList();
            if (dbs.Count > 0) candidates = candidates.Where(c => dbs.Contains(c.Database!)).ToList();
        }
        return candidates.Count == 1 ? candidates[0].Id : null;
    }
}

public static class Scanner
{
    /// <summary>The mechanical pass for one checkout: extract, merge, resolve. Does not save; returns the IDs the pass produced.</summary>
    public static List<string> Run(Atlas atlas, Checkout repo, IEnumerable<string>? previouslyOwned = null, IEnumerable<IExtractor>? extractors = null)
    {
        var x = new Extraction(repo);
        foreach (var extractor in extractors ?? Extractors.All) extractor.Extract(x);
        foreach (var e in x.Entries) e.Verified = new Verified { Commit = repo.Commit7, Date = Clock.Today, Cartographer = Clock.Version };
        Merger.Apply(atlas, repo.Repository, x.Entries, previouslyOwned);
        Resolver.Run(atlas);
        return x.Entries.Select(e => e.Id).ToList();
    }
}

public static class Renamer
{
    /// <summary>Moves an entry, or a whole repository or database when given its repository.md or database.md ID, and rewrites every reference.</summary>
    public static int Run(Atlas atlas, string oldId, string newId)
    {
        if (atlas.Find(oldId) is null) throw new UsageException($"no entry '{oldId}'");
        var whole = oldId.EndsWith("/repository") || oldId.EndsWith("/database");
        if (whole != (newId.EndsWith("/repository") || newId.EndsWith("/database")) || oldId.Split('/')[0] != newId.Split('/')[0])
            throw new UsageException("an entry can only be renamed within its own folder kind, e.g. databases/a/database to databases/b/database");
        var oldArea = Ids.Area(oldId)!;
        var newArea = Ids.Area(newId)!;
        var map = whole
            ? atlas.Entries.Keys.Where(k => k.StartsWith(oldArea + "/")).ToDictionary(k => k, k => newArea + k[oldArea.Length..])
            : new Dictionary<string, string> { [oldId] = newId };
        foreach (var target in map.Values) if (atlas.Entries.ContainsKey(target)) throw new UsageException($"'{target}' already exists");
        string? Map(string? id) => id is not null && map.TryGetValue(id, out var n) ? n : id;
        var oldFolder = oldArea.Split('/')[1] + ":";
        var newFolder = newArea.Split('/')[1] + ":";
        string? Pointer(string? s) => whole && oldArea.StartsWith("repositories/") && s?.StartsWith(oldFolder) == true ? newFolder + s[oldFolder.Length..] : s;

        var touched = 0;
        foreach (var e in atlas.Entries.Values.ToList())
        {
            var before = EntryFile.Write(e);
            e.Repository = Map(e.Repository);
            e.Database = Map(e.Database);
            foreach (var l in e.Links ?? []) { l.To = Map(l.To); l.Via = Map(l.Via); l.Source = Pointer(l.Source); }
            foreach (var s in e.Systems ?? []) { s.Id = Map(s.Id)!; s.Source = Pointer(s.Source); }
            foreach (var p in e.Provides ?? []) p.Source = Pointer(p.Source);
            foreach (var key in e.Sources?.Keys.ToList() ?? []) e.Sources![key] = Pointer(e.Sources[key])!;
            foreach (var (from, to) in map) e.Body = Regex.Replace(e.Body, $@"(?<=Waiting on: entry ){Regex.Escape(from)}(?=\s|$)", to, RegexOptions.Multiline);
            if (map.TryGetValue(e.Id, out var moved)) { atlas.Entries.Remove(e.Id); e.Id = moved; atlas.Entries[moved] = e; touched++; }
            else if (EntryFile.Write(e) != before) touched++;
        }
        atlas.Save();
        if (whole)
        {
            // Files that are not entries (scan-state.yml) move with their folder.
            var from = Path.Combine(atlas.Root, oldArea);
            var to = Path.Combine(atlas.Root, newArea);
            if (Directory.Exists(from))
            {
                foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories).ToList())
                {
                    var target = Path.Combine(to, Path.GetRelativePath(from, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Move(file, target, true);
                }
                Directory.Delete(from, true);
            }
        }
        return touched;
    }
}
