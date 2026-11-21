namespace Cartographer.Cli;

/// <summary>Where a library may be used: text-search candidates for an agent to read and confirm.</summary>
public sealed class UsageCommands : ICommands
{
    const int MaxLines = 200;

    public void Register(Cli cli)
    {
        cli.Add("candidates", "candidates <repository> --library <component id> --path <checkout> [--library-path <checkout>]",
            "Text-search candidates for usage sites of a library's namespaces and types.", Candidates);
    }

    static int Candidates(Args a)
    {
        var atlas = a.Atlas();
        var repo = Checkout.Open(Ids.Slug(a.At(0, "repository")), a.Required("path"));
        var id = a.Required("library");
        var library = atlas.Find(id) is { Kind: Kinds.Component } found ? found : throw new UsageException($"no component '{id}'");
        var namespaces = (library.Provides ?? []).Where(p => p.As == "namespace").Select(p => p.Name).Distinct().ToList();

        // With the library's own checkout, its public type names are searched for too, from the directory of its project file.
        var types = new List<string>();
        if (a.Option("library-path") is { } libraryPath)
        {
            var project = library.Sources?.GetValueOrDefault("proj") is { } pointer && pointer.Contains(':') && pointer.Contains('@')
                ? pointer[(pointer.IndexOf(':') + 1)..pointer.LastIndexOf('@')] : "";
            types = UsageSearch.PublicTypes(Checkout.Open(library.Id.Split('/')[1], libraryPath), project.Contains('/') ? project[..project.LastIndexOf('/')] : "");
        }
        if (namespaces.Count == 0 && types.Count == 0)
            throw new UsageException($"'{id}' provides no namespace{(a.Option("library-path") is null ? "; pass --library-path <checkout> to search for its public type names" : " and no public types were found in its source")}");

        var candidates = UsageSearch.Candidates(repo, namespaces, types);
        Console.WriteLine($"{candidates.Count} candidate line(s) in {candidates.Select(c => c.File).Distinct().Count()} file(s) of {repo.Repository} for {library.Name}" +
            $" (namespaces: {(namespaces.Count > 0 ? string.Join(", ", namespaces) : "none")}; type names: {types.Count}). Text matches only; read each to confirm.");
        foreach (var file in candidates.Take(MaxLines).GroupBy(c => c.File))
        {
            Console.WriteLine(file.Key);
            foreach (var (path, line, text) in file) Console.WriteLine($"  {path}:{line}: {(text.Length > 160 ? text[..160] + "…" : text)}");
        }
        if (candidates.Count > MaxLines) Console.WriteLine($"… and {candidates.Count - MaxLines} more candidate line(s) not shown");
        return 0;
    }
}
