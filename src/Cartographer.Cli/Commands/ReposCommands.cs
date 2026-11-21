namespace Cartographer.Cli;

/// <summary>Repositories at the SCMs and the clone cache.</summary>
public sealed class ReposCommands : ICommands
{
    public void Register(Cli cli)
    {
        cli.Add("repos list", "repos list [--refresh]", "List repositories with their linked locations; --refresh asks GitHub and Azure DevOps.", List);
        cli.Add("clone", "clone <repository>", "Fetch or update the cached partial clone; print its path.", a =>
        {
            Console.WriteLine(Clones.Ensure(a.Atlas(), Ids.Slug(a.At(0, "repository"))));
            return 0;
        });
        cli.Add("cache clear", "cache clear", "Delete the clone cache.", _ =>
        {
            Clones.Clear();
            Console.WriteLine($"cleared {Clones.CacheRoot}");
            return 0;
        });
    }

    static int List(Args a)
    {
        var atlas = a.Atlas();
        var listing = a.Flag("refresh") ? Repos.Refresh(atlas, Scm.FromEnvironment(atlas.Config)).GetAwaiter().GetResult() : null;
        Console.Write(Repos.Table(atlas));
        if (listing is null) return 0;
        foreach (var r in listing.Skipped) Console.WriteLine($"skipped ({r.Skipped}): {r.Id}");
        foreach (var folder in listing.Excluded) Console.WriteLine($"excluded: {folder}");
        foreach (var r in listing.Repositories.Where(r => r.Question is not null)) Console.WriteLine($"not linked: {r.Question}");
        return 0;
    }
}
