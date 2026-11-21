using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Cartographer;

public sealed class EntryFormatException(string message) : Exception(message);

/// <summary>Reads and writes one entry file: YAML frontmatter, then a Markdown body.</summary>
public static class EntryFile
{
    public static readonly ISerializer Yaml = new SerializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .WithQuotingNecessaryStrings()
        .WithIndentedSequences()
        .DisableAliases()
        .Build();

    public static readonly IDeserializer Reader = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .Build();

    public static Entry Parse(string id, string text)
    {
        text = text.Replace("\r\n", "\n");
        if (!text.StartsWith("---\n")) throw new EntryFormatException("the file does not start with a frontmatter block (---)");
        var end = text.IndexOf("\n---\n", 3, StringComparison.Ordinal);
        if (end < 0 && text.EndsWith("\n---")) end = text.Length - 4;
        if (end < 0) throw new EntryFormatException("the frontmatter block is not closed (---)");
        Entry entry;
        try { entry = Reader.Deserialize<Entry>(text[4..(end + 1)]) ?? new Entry(); }
        catch (YamlException e) { throw new EntryFormatException($"frontmatter line {e.Start.Line}: {e.InnerException?.Message ?? e.Message}"); }
        entry.Id = id;
        entry.Body = text[Math.Min(text.Length, end + 5)..].Trim('\n');
        return entry;
    }

    public static string Write(Entry entry)
    {
        var body = entry.Body.Trim('\n');
        return "---\n" + Yaml.Serialize(entry) + "---\n" + (body.Length == 0 ? "" : "\n" + body + "\n");
    }
}

/// <summary>The body's fixed sections.</summary>
public static class Sections
{
    public const string Overview = "Overview", Technical = "Technical detail", Questions = "Open questions";
    public static readonly string[] Order = [Overview, Technical, Questions];
    const string Open = "<!-- extracted -->", Close = "<!-- /extracted -->";

    /// <summary>Section title to its text, in the order found. Text before the first heading is under "".</summary>
    public static List<(string Title, string Text)> Split(string body)
    {
        var result = new List<(string, string)>();
        var title = "";
        var lines = new List<string>();
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("## "))
            {
                if (title != "" || lines.Any(l => l.Trim() != "")) result.Add((title, string.Join('\n', lines).Trim('\n')));
                title = line[3..].Trim();
                lines = [];
            }
            else lines.Add(line);
        }
        if (title != "" || lines.Any(l => l.Trim() != "")) result.Add((title, string.Join('\n', lines).Trim('\n')));
        return result;
    }

    public static string Get(string body, string title) => Split(body).FirstOrDefault(s => s.Title == title).Text ?? "";

    /// <summary>Replaces one section, keeping the fixed order; empty text removes the section.</summary>
    public static string Set(string body, string title, string text)
    {
        var parts = Split(body).Where(s => s.Title != title).ToList();
        if (text.Trim().Length > 0) parts.Add((title, text.Trim('\n')));
        int Rank((string Title, string Text) s) => s.Title == "" ? -1 : Array.IndexOf(Order, s.Title) is var i and >= 0 ? i : Order.Length;
        return string.Join("\n\n", parts.OrderBy(Rank).Select(s => s.Title == "" ? s.Text : $"## {s.Title}\n\n{s.Text}"));
    }

    /// <summary>Replaces the CLI-owned block at the top of Technical detail; null or empty removes it.</summary>
    public static string SetExtracted(string body, string? extracted)
    {
        var technical = Get(body, Technical);
        var start = technical.IndexOf(Open, StringComparison.Ordinal);
        var end = technical.IndexOf(Close, StringComparison.Ordinal);
        var rest = start >= 0 && end > start ? (technical[..start] + technical[(end + Close.Length)..]).Trim('\n') : technical;
        var block = string.IsNullOrWhiteSpace(extracted) ? "" : $"{Open}\n{extracted.Trim('\n')}\n{Close}";
        return Set(body, Technical, string.Join("\n\n", new[] { block, rest }.Where(s => s.Length > 0)));
    }
}
