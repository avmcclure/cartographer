using System.Text;
using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>Order 30: classic ASP sites, the COM classes their pages create by ProgID, and their include graph.</summary>
public sealed class AspExtractor : IExtractor
{
    // ponytail: also matches CreateObject in client-side VBScript blocks; tell server from client script if that ever shows up as noise.
    static readonly Regex Create = new(@"\bCreateObject\s*\(\s*([^)\r\n]*)", RegexOptions.IgnoreCase);
    static readonly Regex LiteralProgId = new(@"^([""'])([A-Za-z_][\w.]*)\1\s*(,.*)?$");
    static readonly Regex ObjectTag = new(@"<object\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    static readonly Regex Include = new(@"<!--\s*#include\s+(file|virtual)\s*=\s*[""']?([^""'>\s]+)[""']?\s*-->", RegexOptions.IgnoreCase);
    static readonly Regex Platform = new(@"^((ADODB|ADOX|Scripting|MSXML\d?|WScript|CDO|CDONTS)\.|Microsoft\.XML(HTTP|DOM)$)", RegexOptions.IgnoreCase);

    public int Order => 30;

    public void Extract(Extraction x)
    {
        var files = x.Repo.WithExtension(".asp", ".asa", ".inc").ToList();
        static bool IsPage(string f) => f.EndsWith(".asp", StringComparison.OrdinalIgnoreCase);
        static bool Under(string f, string dir) => dir == "" || f.StartsWith(dir + "/", StringComparison.Ordinal);

        // A site is rooted at the top-most directory holding ASP files that no earlier component owns, so parents go first.
        foreach (var dir in files.Where(f => x.ComponentAt(f) is null).Select(LegacyText.Directory).Distinct()
                     .OrderBy(d => d.Count(ch => ch == '/')).ThenBy(d => d, StringComparer.Ordinal).ToList())
        {
            if (x.ComponentAt(dir == "" ? "x" : dir + "/x") is not null) continue;
            if (!files.Any(f => IsPage(f) && Under(f, dir) && x.ComponentAt(f) is null)) continue;   // includes alone are not a site
            x.Component(dir, dir == "" ? x.Repo.Repository : dir[(dir.LastIndexOf('/') + 1)..], "site");
        }

        foreach (var group in files.GroupBy(f => x.ComponentAt(f)).Where(g => g.Key is not null))
        {
            var c = group.Key!;
            var root = x.ComponentRoots.First(r => r.Value == c).Key;
            var platform = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var runTime = new List<string>();
            var includedBy = new SortedDictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in group)
            {
                var text = LegacyText.Read(x.Repo, file);
                var created = new List<(int Index, string? ProgId)>();
                foreach (Match m in Create.Matches(text))
                {
                    var lineStart = text.LastIndexOf('\n', m.Index) + 1;
                    var before = text[lineStart..m.Index].TrimStart();
                    if (before.StartsWith('\'') || before.StartsWith("//") || before.StartsWith("rem ", StringComparison.OrdinalIgnoreCase)) continue;
                    var argument = m.Groups[1].Value.Trim();
                    if (argument.Length > 0) created.Add((m.Index, LiteralProgId.Match(argument) is { Success: true } literal ? literal.Groups[2].Value : null));
                }
                foreach (Match m in ObjectTag.Matches(text))
                    if (Regex.IsMatch(m.Value, @"\brunat\s*=\s*[""']?server", RegexOptions.IgnoreCase) && Regex.Match(m.Value, @"\bprogid\s*=\s*[""']?([\w.]+)", RegexOptions.IgnoreCase) is { Success: true } tag)
                        created.Add((m.Index, tag.Groups[1].Value));
                foreach (var (index, progId) in created.OrderBy(o => o.Index))
                {
                    var line = LegacyText.Line(text, index);
                    if (progId is null) runTime.Add($"`{file}` line {line}");
                    else if (Platform.IsMatch(progId)) platform.Add(progId);
                    else LegacyText.Link(x, c, "calls", progId, "com", file, line);
                }
                foreach (Match m in Include.Matches(text))
                {
                    // "file" is relative to the including file; "virtual" starts at the site root, as far as a checkout can tell.
                    var written = m.Groups[2].Value;
                    var target = (m.Groups[1].Value.Equals("file", StringComparison.OrdinalIgnoreCase)
                        ? LegacyText.Find(x.Repo, LegacyText.Directory(file), written)
                        : LegacyText.Find(x.Repo, root, written.TrimStart('/', '\\'))) ?? written.Replace('\\', '/');
                    if (!includedBy.TryGetValue(target, out var by)) includedBy[target] = by = [];
                    by.Add(file);
                }
            }

            LegacyText.AddStack(c, "classic-asp");
            var block = new StringBuilder();
            var others = group.Count(f => !IsPage(f));
            var globalAsa = group.Any(f => Path.GetFileName(f).Equals("global.asa", StringComparison.OrdinalIgnoreCase));
            block.Append($"- Classic ASP: {LegacyText.Plural(group.Count(IsPage), "page", "pages")}{(others > 0 ? $", {LegacyText.Plural(others, "other script file", "other script files")} (`.asa`, `.inc`)" : "")}; `global.asa` {(globalAsa ? "present" : "not present")}.\n");
            if (includedBy.Count > 0)
            {
                var most = includedBy.OrderByDescending(i => i.Value.Count).ThenBy(i => i.Key, StringComparer.Ordinal).Take(5).Select(i => $"`{i.Key}` ({i.Value.Count})");
                block.Append($"- Includes: {LegacyText.Plural(includedBy.Count, "file is", "files are")} included by others. Most included, with the number of files including each: {string.Join(", ", most)}.\n");
            }
            if (platform.Count > 0) block.Append($"- Platform objects created: {string.Join(", ", platform.Select(p => $"`{p}`"))}.\n");
            if (runTime.Count > 0)
                block.Append($"- {LegacyText.Plural(runTime.Count, "object created from a name", "objects created from names")} built at run time, not linked: {string.Join(", ", runTime.Take(5))}{(runTime.Count > 5 ? $" and {runTime.Count - 5} more" : "")}.\n");
            LegacyText.Append(c, block.ToString());
        }
    }
}
