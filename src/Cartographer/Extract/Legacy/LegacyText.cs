using System.Text;
using System.Text.RegularExpressions;

namespace Cartographer;

/// <summary>What the pre-.NET extractors share: reading old files, line numbers, source keys and adding cited links.</summary>
static class LegacyText
{
    static readonly Encoding Utf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);
    static readonly Encoding Windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

    /// <summary>Reads a file of the checkout as UTF-8, or as Windows-1252 when it is not valid UTF-8.</summary>
    public static string Read(Checkout repo, string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(repo.Root, path));
        try { return Utf8.GetString(bytes).TrimStart('﻿'); }
        catch (DecoderFallbackException) { return Windows1252.GetString(bytes); }
    }

    public static int Line(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    public static string Directory(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";

    /// <summary>A source key for a file: its name in lower case with hyphens, e.g. "invoice-cls"; "proj" for a project file.</summary>
    public static string Key(string path) => path.EndsWith(".vbp", StringComparison.OrdinalIgnoreCase) ? "proj" : Regex.Replace(Path.GetFileName(path).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') is { Length: > 0 } key ? key : "file";

    /// <summary>The file of the checkout that a path written in a file refers to (Windows separators, "..", any capitalisation), or null.</summary>
    public static string? Find(Checkout repo, string fromDirectory, string relative)
    {
        var parts = new List<string>();
        foreach (var part in $"{fromDirectory}/{relative.Replace('\\', '/')}".Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part == ".") continue;
            if (part != "..") parts.Add(part);
            else if (parts.Count == 0) return null;
            else parts.RemoveAt(parts.Count - 1);
        }
        var path = string.Join('/', parts);
        return repo.Files.FirstOrDefault(f => f.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Adds an unresolved extracted link citing a file and line, unless the entry already has that link.</summary>
    public static void Link(Extraction x, Entry entry, string type, string name, string? how, string path, int line)
    {
        var link = new Link { Type = type, Name = name, How = how, Basis = Basis.Extracted };
        if (entry.Links?.Any(l => l.Identity == link.Identity) == true) return;
        link.Source = x.Cite(entry, Key(path), path, line);
        Extraction.AddLink(entry, link);
    }

    /// <summary>Adds lines to an entry's extracted block, after whatever an earlier extractor wrote.</summary>
    public static void Append(Entry entry, string lines) => entry.Extracted = (entry.Extracted is null ? "" : entry.Extracted.TrimEnd('\n') + "\n") + lines;

    public static void AddStack(Entry entry, string technology)
    {
        if (!(entry.Stack ??= []).Contains(technology)) entry.Stack.Add(technology);
    }

    public static string Plural(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n} {many}";
}
