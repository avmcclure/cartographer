using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static Cartographer.DotNetFiles;

namespace Cartographer;

/// <summary>
/// Order 50: connection strings and WCF endpoints from web.config, app.config, *.exe.config and appsettings*.json,
/// attached to the component the file belongs to. From a connection string only the server and database names are kept;
/// no other value from it is ever written.
/// </summary>
public sealed class ConfigExtractor : IExtractor
{
    static readonly string[] ServerKeys = ["data source", "server", "address", "addr", "network address", "host"];
    static readonly string[] DatabaseKeys = ["initial catalog", "database"];
    static readonly Regex CredentialKey = new("password|pwd|key|secret|token|signature", RegexOptions.IgnoreCase);

    public int Order => 50;

    public void Extract(Extraction x)
    {
        var files = Index(x.Repo);
        foreach (var path in x.Repo.Files)
        {
            var file = Path.GetFileName(path).ToLowerInvariant();
            var xml = file is "web.config" or "app.config" || file.EndsWith(".exe.config");
            var json = file.StartsWith("appsettings") && file.EndsWith(".json");
            if (!xml && !json) continue;
            if (x.ComponentAt(path) is not { } c) continue;
            var notes = new List<string>();
            if (xml) ReadXml(x, c, path, files, notes); else ReadJson(x, c, path, notes);
            c.Extracted += string.Concat(notes.Select(n => $"- {n}\n"));
        }
    }

    static void ReadXml(Extraction x, Entry c, string path, Dictionary<string, string> files, List<string> notes)
    {
        if (Load(x.Repo, path) is not { } root) return;

        // <connectionStrings configSource="other.config"/> keeps the strings in a file beside this one.
        var connections = root.Children("connectionStrings").FirstOrDefault();
        var connectionsPath = path;
        if (connections is not null && Attr(connections, "configSource") is { } source && Resolve(Dir(path), source) is { } resolved && files.TryGetValue(resolved, out var other))
            (connections, connectionsPath) = (Load(x.Repo, other), other);
        foreach (var add in connections.Children("add"))
            Connection(x, c, connectionsPath, Line(add), Attr(add, "name") ?? "(unnamed)", Attr(add, "connectionString") ?? "", notes);

        var serviceModel = root.Children("system.serviceModel").FirstOrDefault();
        foreach (var endpoint in serviceModel.Children("client").Elements().Named("endpoint"))
        {
            if (Contract(endpoint) is not { } contract) continue;
            Extraction.AddLink(c, new Link { Type = "calls", Name = contract, How = "wcf", Basis = Basis.Extracted, Source = x.Cite(c, "config", path, Line(endpoint)) });
            notes.Add($"WCF client endpoint (`{path}` line {Line(endpoint)}): contract {Tick(contract)}, {Endpoint(endpoint)}.");
        }
        foreach (var service in serviceModel.Children("services").Elements().Named("service"))
            foreach (var endpoint in service.Children("endpoint"))
            {
                if (Contract(endpoint) is not { } contract) continue;
                c.Provides ??= [];
                if (!c.Provides.Any(p => p.Name == contract && p.As == "contract"))
                    c.Provides.Add(new Provided { Name = contract, As = "contract", Basis = Basis.Extracted, Source = x.Cite(c, "config", path, Line(endpoint)) });
                notes.Add($"WCF service {Tick(Attr(service, "name"))} (`{path}` line {Line(endpoint)}): contract {Tick(contract)}, {Endpoint(endpoint)}.");
            }
    }

    /// <summary>An endpoint's contract, leaving out the metadata endpoint every WCF service has.</summary>
    static string? Contract(XElement endpoint) => Attr(endpoint, "contract") is { } c && c != "IMetadataExchange" ? c : null;

    static string Endpoint(XElement endpoint) =>
        $"binding {Attr(endpoint, "binding") ?? "not given"}, address {Tick(Attr(endpoint, "address") is { } a ? SafeUrl(a) : null)}";

    static void ReadJson(Extraction x, Entry c, string path, List<string> notes)
    {
        var text = x.Repo.Read(path);
        JsonDocument document;
        try { document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }); }
        catch (JsonException) { return; }
        using var _ = document;
        if (document.RootElement.ValueKind != JsonValueKind.Object) return;
        var section = document.RootElement.EnumerateObject().FirstOrDefault(p => p.Name.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase));
        if (section.Value.ValueKind != JsonValueKind.Object) return;
        var sectionStart = Math.Max(0, text.IndexOf($"\"{section.Name}\"", StringComparison.Ordinal));
        foreach (var p in section.Value.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String))
        {
            // ponytail: the line is found by searching for the quoted name after the section starts; a JSON reader that reports positions if this ever points at the wrong line.
            var at = text.IndexOf($"\"{p.Name}\"", sectionStart, StringComparison.Ordinal);
            Connection(x, c, path, text.Take(Math.Max(0, at)).Count(ch => ch == '\n') + 1, p.Name, p.Value.GetString() ?? "", notes);
        }
    }

    static void Connection(Extraction x, Entry c, string path, int line, string name, string connectionString, List<string> notes)
    {
        var where = $"Connection string {Tick(name)} (`{path}` line {line})";
        if (ParseConnectionString(connectionString) is not { } parsed) { notes.Add($"{where}: not in key=value form; nothing recorded."); return; }
        notes.Add($"{where}: server {Tick(parsed.Server)}, database {Tick(parsed.Database)}, {(parsed.Credential ? "a credential is present (not recorded)" : "no credential")}.");
        if (parsed.Database is not null)
            Extraction.AddLink(c, new Link { Type = "uses", Name = parsed.Database, Basis = Basis.Extracted, Source = x.Cite(c, "config", path, line) });
    }

    /// <summary>Server and database from a connection string, and whether it holds a credential. Null when it cannot be parsed. Looks inside an Entity Framework "provider connection string".</summary>
    internal static (string? Server, string? Database, bool Credential)? ParseConnectionString(string text)
    {
        var builder = new DbConnectionStringBuilder();
        try { builder.ConnectionString = text; }
        catch (ArgumentException) { return null; }
        if (builder.TryGetValue("provider connection string", out var inner)) return ParseConnectionString(inner.ToString() ?? "");
        string? First(string[] keys) => keys.Select(k => builder.TryGetValue(k, out var v) ? v.ToString()?.Trim() : null).FirstOrDefault(v => !string.IsNullOrEmpty(v));
        return (First(ServerKeys), First(DatabaseKeys), builder.Keys.Cast<string>().Any(CredentialKey.IsMatch));
    }
}
