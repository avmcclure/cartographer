using System.Xml;
using System.Xml.Linq;

namespace Cartographer;

/// <summary>Small helpers the .NET extractors share: XML with line numbers, and paths inside a checkout.</summary>
static class DotNetFiles
{
    /// <summary>Parses a checkout file as XML, keeping line numbers. Null when it is not well-formed. Never follows DTDs or external entities.</summary>
    public static XElement? Load(Checkout repo, string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(new StringReader(repo.Read(path)), settings);
            return XDocument.Load(reader, LoadOptions.SetLineInfo).Root;
        }
        catch (XmlException) { return null; }
    }

    public static int Line(XElement e) => ((IXmlLineInfo)e).LineNumber;

    /// <summary>An attribute's trimmed value, ignoring XML namespaces and case; null when absent or empty.</summary>
    public static string? Attr(XElement e, string name) =>
        e.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() is { Length: > 0 } v ? v : null;

    /// <summary>Elements with this local name, ignoring XML namespaces (legacy project files have one, SDK-style ones do not) and case.</summary>
    public static IEnumerable<XElement> Named(this IEnumerable<XElement> elements, string name) =>
        elements.Where(e => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<XElement> Children(this XElement? e, string name) => e is null ? [] : e.Elements().Named(name);

    /// <summary>The directory part of a checkout path; "" for a file at the root.</summary>
    public static string Dir(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";

    public static string Join(string dir, string name) => dir == "" ? name : $"{dir}/{name}";

    /// <summary>A directory, then each directory above it, ending with "" (the checkout root).</summary>
    public static IEnumerable<string> Up(string dir)
    {
        for (; ; dir = Dir(dir))
        {
            yield return dir;
            if (dir == "") yield break;
        }
    }

    /// <summary>A path written in a project file ("..\lib\X.dll"), made relative to the checkout root. Null when it leaves the checkout.</summary>
    public static string? Resolve(string dir, string relative)
    {
        var parts = new List<string>(dir.Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var part in relative.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part != "..") parts.Add(part);
            else if (parts.Count == 0) return null;
            else parts.RemoveAt(parts.Count - 1);
        }
        return string.Join('/', parts);
    }

    /// <summary>Checkout files by path, ignoring case, so "Web.config" and "web.config" are both found.</summary>
    public static Dictionary<string, string> Index(Checkout repo)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in repo.Files) index.TryAdd(f, f);
        return index;
    }

    /// <summary>A URL without the parts that can carry a credential: user:password@ and the query string. Other text is returned as it is.</summary>
    public static string SafeUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.UserInfo.Length > 0 || u.Query.Length > 0)
            ? u.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped)
            : url;

    /// <summary>Text from a checkout made safe to put between backticks on one line.</summary>
    public static string Tick(string? text) => text is null ? "not given" : $"`{text.Replace('`', '\'').ReplaceLineEndings(" ")}`";
}
