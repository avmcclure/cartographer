namespace Cartographer.Cli;

/// <summary>Live SQL Server instances: reading their catalogs, and showing a DBA exactly what is read and with which grants.</summary>
public sealed class DbCommands : ICommands
{
    public void Register(Cli cli)
    {
        cli.Add("db queries", "db queries", "Print the fixed read-only catalog queries a live read runs.", _ => Print(CatalogQueries.Print()));
        cli.Add("db role-script", "db role-script", "Print a T-SQL script creating a least-privilege role for live reads.", _ => Print(CatalogQueries.RoleScript));
        cli.Add("db read", "db read <instance> [--database <name>]", "Read a listed instance's catalogs into entries; records fingerprint, permission level and drift.", a =>
        {
            var atlas = a.Atlas();
            var (code, text) = LiveDb.Read(atlas, a.At(0, "instance"), a.Option("database"));
            Console.Write(text);
            Indexer.Write(atlas);
            return CoreCommands.Check(atlas) == 0 ? code : 1;
        });
    }

    static int Print(string text) { Console.Write(text); return 0; }
}
