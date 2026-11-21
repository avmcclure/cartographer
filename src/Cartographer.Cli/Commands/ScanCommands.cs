namespace Cartographer.Cli;

/// <summary>The mechanical pass.</summary>
public sealed class ScanCommands : ICommands
{
    public void Register(Cli cli)
    {
        cli.Add("scan", "scan <repository> --path <checkout>", "Mechanical pass for one repository, then resolve, validate and index.", Scan);
    }

    static int Scan(Args a)
    {
        var atlas = a.Atlas();
        var repository = Ids.Slug(a.At(0, "repository"));
        var produced = Scanner.Run(atlas, Checkout.Open(repository, a.Required("path")));
        var changed = atlas.Save();
        Indexer.Write(atlas);
        Console.WriteLine($"{repository}: {produced.Count} entries produced, {changed.Count} files changed");
        return CoreCommands.Check(atlas) == 0 ? 0 : 1;
    }
}
