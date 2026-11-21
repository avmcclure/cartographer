using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>Text search for places that may use a library. Nothing is built or resolved: these are candidates for a reader to confirm.</summary>
public static class UsageSearch
{
    static readonly string[] Extensions = [".cs", ".vb", ".cshtml", ".aspx", ".razor"];
    // ponytail: regexes, not parsers. They miss types behind unusual modifiers and count nested public types; use Roslyn if exact lists are needed.
    static readonly Regex CSharpType = new(@"\bpublic\s+(?:(?:static|sealed|abstract|partial|readonly|ref|unsafe|new)\s+)*(?:class|interface|struct|enum|record(?:\s+(?:class|struct))?|delegate\s+[^\s(]+)\s+(\w+)");
    static readonly Regex VbType = new(@"^\s*Public\s+(?:(?:MustInherit|NotInheritable|Partial|Shadows)\s+)*(?:Class|Interface|Structure|Enum|Module|Delegate\s+(?:Sub|Function))\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>Names of the public types declared in C# and VB source under a directory of a checkout ("" for all of it).</summary>
    public static List<string> PublicTypes(Checkout library, string directory)
    {
        var prefix = directory.Trim('/') is { Length: > 0 } d ? d + "/" : "";
        return library.WithExtension(".cs", ".vb").Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => (f.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) ? VbType : CSharpType).Matches(library.Read(f)).Select(m => m.Groups[1].Value))
            .Distinct().Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Lines that mention one of the namespaces (a using or Imports line, or a qualified name) or one of the type names as a whole word.
    /// ponytail: a type name is matched wherever it appears, so a common name such as "Order" gives false candidates.
    /// </summary>
    public static List<(string File, int Line, string Text)> Candidates(Checkout repo, IEnumerable<string> namespaces, IEnumerable<string> types)
    {
        var patterns = namespaces.Select(n => $@"(?<![\w.]){Regex.Escape(n)}(?!\w)").Concat(types.Select(t => $@"\b{Regex.Escape(t)}\b")).ToList();
        var found = new List<(string, int, string)>();
        if (patterns.Count == 0) return found;
        var exact = new Regex(string.Join('|', patterns));
        var anyCase = new Regex(exact.ToString(), RegexOptions.IgnoreCase);   // VB ignores case
        foreach (var file in repo.WithExtension(Extensions))
        {
            var regex = file.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) ? anyCase : exact;
            var lines = repo.Read(file).Split('\n');
            for (var i = 0; i < lines.Length; i++)
                if (regex.IsMatch(lines[i])) found.Add((file, i + 1, lines[i].Trim()));
        }
        return found;
    }
}
