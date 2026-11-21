namespace Cartographer.Cli;

/// <summary>Atlas integrity and read-only queries.</summary>
public sealed class CoreCommands : ICommands
{
    public void Register(Cli cli)
    {
        cli.Add("init", "init <dir> --organisation <name> [--github <org>] [--azdo <org>]", "Create an atlas.", Init);
        cli.Add("check", "check", "Validate every entry.", a => Check(a.Atlas()));
        cli.Add("index", "index", "Rebuild _generated/.", a => { Indexer.Write(a.Atlas()); return 0; });
        cli.Add("rename", "rename <old id> <new id>", "Move an entry and rewrite links to it.", a =>
        {
            var atlas = a.Atlas();
            Console.WriteLine($"{Renamer.Run(atlas, a.At(0, "old id"), a.At(1, "new id"))} entries rewritten");
            Indexer.Write(Atlas.Load(atlas.Root));
            return 0;
        });
        cli.Add("find", "find <text>", "Find entries by name, ID, provided name or summary.", a => Print(Queries.Find(a.Atlas(), a.At(0, "text"))));
        cli.Add("show", "show <id>", "An entry's summary, links and overview.", a => Print(Queries.Show(a.Atlas(), a.At(0, "id"))));
        cli.Add("used-by", "used-by <id>", "What links directly to an entry.", a => Print(Queries.UsedBy(a.Atlas(), a.At(0, "id"))));
        cli.Add("impact", "impact <id> [--depth n]", "What could be affected by changing an entry.", a => Print(Queries.Impact(a.Atlas(), a.At(0, "id"), a.Int("depth", 3))));
        cli.Add("question add", "question add <entry> --missing … --waiting-on … --answer-looks-like … [--raised-by agent]", "Add an open question.", QuestionAdd);
        cli.Add("question attempt", "question attempt <entry> <q>", "Record a failed attempt to answer.", QuestionAttempt);
    }

    static int Print(string text) { Console.Write(text); return 0; }

    public static int Check(Atlas atlas)
    {
        var issues = Validator.Check(atlas);
        foreach (var i in issues.OrderBy(i => !i.Error).ThenBy(i => i.Id, StringComparer.Ordinal)) Console.WriteLine(i);
        var errors = issues.Count(i => i.Error);
        Console.WriteLine($"{atlas.Entries.Count} entries, {errors} error(s), {issues.Count - errors} warning(s)");
        return errors > 0 ? 1 : 0;
    }

    static int Init(Args a)
    {
        var dir = Path.GetFullPath(a.At(0, "directory"));
        if (File.Exists(Path.Combine(dir, Atlas.ConfigFile))) throw new UsageException($"{dir} is already an atlas");
        Directory.CreateDirectory(dir);
        var organisation = a.Required("organisation");
        static string Scm(string key, string? org) => org is null ? $"# {key}: {{organisation: <name>}}\n" : $"{key}: {{organisation: {org}}}\n";
        File.WriteAllText(Path.Combine(dir, Atlas.ConfigFile),
            $"organisation: {organisation}\n" + Scm("github", a.Option("github")) + Scm("azure-devops", a.Option("azdo")) +
            "exclude: []            # repository name patterns to leave out, e.g. \"*-archive\"\n" +
            "internal-prefixes: []  # package name patterns that are always internal, e.g. \"Acme.*\"\n" +
            "locations: {}          # <repository id>: [location ids], when the naming rule cannot link them\n" +
            "instances: []          # live SQL Server instances that may be read; see docs/spec/v1.md section 9\n");
        File.WriteAllText(Path.Combine(dir, "AGENTS.md"), AgentsFile.Replace("{organisation}", organisation));
        File.WriteAllText(Path.Combine(dir, ".gitignore"), "site/\n");
        if (!Directory.Exists(Path.Combine(dir, ".git"))) Checkout.Git(dir, "init", "-b", "main");
        Indexer.Write(Atlas.Load(dir));
        Console.WriteLine($"created an atlas for {organisation} at {dir}");
        return 0;
    }

    static int QuestionAdd(Args a)
    {
        var atlas = a.Atlas();
        var entry = atlas.Find(a.At(0, "entry id")) ?? throw new UsageException($"no entry '{a.At(0, "entry id")}'");
        var raisedBy = a.Option("raised-by") ?? "agent";
        if (!Question.Raisers.Contains(raisedBy)) throw new UsageException("--raised-by must be cli, agent or person");
        Question? added = null;
        QuestionList.Update(entry, list => added = list.Add(a.Required("missing"), a.Required("waiting-on"), a.Required("answer-looks-like"), raisedBy));
        if (added is null) { Console.WriteLine("already asked on this entry; nothing added"); return 1; }
        atlas.Save();
        Console.WriteLine($"added {added.Id} to {entry.Id}");
        return 0;
    }

    static int QuestionAttempt(Args a)
    {
        var atlas = a.Atlas();
        var entry = atlas.Find(a.At(0, "entry id")) ?? throw new UsageException($"no entry '{a.At(0, "entry id")}'");
        var id = a.At(1, "question id");
        var state = "";
        QuestionList.Update(entry, list =>
        {
            var q = list.Items.FirstOrDefault(q => q.Id == id) ?? throw new UsageException($"{entry.Id} has no question {id}");
            q.Attempts++;
            // Questions a person raised are never given up on by an agent's count alone, but they do go back to a person.
            if (q.State == "open" && q.Attempts >= atlas.Config.Limits.QuestionAttempts) q.State = "needs-person";
            state = $"{q.Id}: {q.Attempts} attempt(s), {q.State}";
        });
        atlas.Save();
        Console.WriteLine(state);
        return 0;
    }

    const string AgentsFile = """
        # Atlas of {organisation}

        This repository is a Cartographer atlas: a map of {organisation}'s repositories, components, databases and the links between them. Every file is an entry about one thing.

        ## Reading it with little context

        1. Read `_generated/index.md`.
        2. Open one folder index under `_generated/`.
        3. Open one entry and read its frontmatter `summary` and its Overview.
        4. Read its Technical detail, and the source files it points to, only if you need them.

        An entry's ID is its path without `.md`. What uses an entry is in `_generated/<id>.used-by.md`.
        With the `cartographer` CLI installed: `cartographer find <text>`, `show <id>`, `used-by <id>`, `impact <id>`.

        ## Rules

        - Every link says how it is known: `extracted` (read mechanically), `inferred` (concluded by an agent) or `stated` (said by a person).
        - Never change or remove a `stated` fact, or text between `<!-- person -->` markers.
        - Never write a secret value into an entry.
        - What you cannot determine goes in the entry's Open questions section, not in prose.
        - Do not edit `_generated/`, `scan-state.yml`, `changes/` or `layouts/`; the CLI writes them.
        - Run `cartographer check` before committing.
        """;
}
