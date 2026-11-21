namespace Cartographer.Cli;

/// <summary>Scan sessions: the mechanical pass, what is due, what changed, finishing a repository and the session's branch.</summary>
public sealed class ScanCommands : ICommands
{
    public void Register(Cli cli)
    {
        cli.Add("scan", "scan <repository> [--path <checkout>] [--full] | scan --all [--max <n>]", "Mechanical pass for one repository, or every due one; then resolve, validate and index.", Scan);
        cli.Add("changed", "changed <repository> [--path <checkout>]", "Files changed since the last scanned commit, grouped by component.", a => Print(Scans.Changed(a.Atlas(), Repository(a), a.Option("path"))));
        cli.Add("repos due", "repos due", "Repositories due for a scan, each with its reasons.", a =>
        {
            var due = Scans.Due(a.Atlas());
            foreach (var d in due) Console.WriteLine($"{d.Folder}: {string.Join("; ", d.Reasons)}");
            Console.WriteLine($"{due.Count} repositories due");
            return 0;
        });
        cli.Add("done", "done <repository> <component>", "Mark one component's entries complete in the scan in progress.", a =>
        {
            var atlas = a.Atlas();
            var state = ScanState.Load(atlas, Scans.Folder(a.At(0, "repository")));
            var component = a.At(1, "component").Split('/')[^1];
            if (state.InProgress is null) throw new UsageException($"{a.At(0, "repository")} has no scan in progress");
            if (!state.InProgress.Components.ContainsKey(component)) throw new UsageException($"no component '{component}' in this scan; it has: {string.Join(", ", state.InProgress.Components.Keys)}");
            state.InProgress.Components[component] = "done";
            state.Save(atlas);
            Console.WriteLine($"{component}: done; {state.InProgress.Components.Count(c => c.Value != "done")} still pending");
            return 0;
        });
        cli.Add("finish", "finish <repository> [--force]", "Mark the repository's scan complete, write its change summary and commit its files on the scan branch.", a =>
        {
            var (code, text) = Scans.Finish(a.Atlas(), Repository(a), a.Flag("force"));
            Console.Write(text);
            return code;
        });
        cli.Add("question retry", "question retry", "Open questions whose 'Waiting on' target has changed, or that a person answered.", a =>
        {
            var items = QuestionRetry.Find(a.Atlas());
            foreach (var i in items) Console.WriteLine($"{i.Entry} {i.Question}: {i.Reason}");
            Console.WriteLine($"{items.Count} questions to retry");
            return 0;
        });
        cli.Add("session commit", "session commit --message <text>", "Commit work that is not one repository's finished scan, on the scan branch.", a =>
        {
            var atlas = a.Atlas();
            if (CoreCommands.Check(atlas) != 0) return 1;
            Indexer.Write(atlas);
            return Print(Sessions.Commit(atlas.Root, a.Required("message")) + "\n");
        });
        cli.Add("session start", "session start [--branch <name>]", "Create the scan branch in the atlas and switch to it.", a => Print(Sessions.Start(a.Atlas().Root, a.Option("branch")) + "\n"));
        cli.Add("session end", "session end [--pull-request]", "Leave the scan branch local, or push it and open a pull request joining its change summaries.", a => Print(Sessions.End(a.Atlas().Root, a.Flag("pull-request"))));
    }

    static int Print(string text) { Console.Write(text); return 0; }
    static string Repository(Args a) => Scans.Folder(a.At(0, "repository"));

    static int Scan(Args a)
    {
        var atlas = a.Atlas();
        var failed = 0;
        if (a.Flag("all"))
        {
            foreach (var due in Scans.Due(atlas).Take(a.Int("max", int.MaxValue)))
            {
                // One repository that cannot be cloned or read must not stop the sweep.
                try { Console.WriteLine(Scans.Run(atlas, due.Folder)); }
                catch (Exception e) { failed++; Console.WriteLine($"{due.Folder}: failed: {e.Message}"); }
            }
            if (failed > 0) Console.WriteLine($"{failed} repositories failed");
        }
        else Console.WriteLine(Scans.Run(atlas, Repository(a), a.Option("path"), a.Flag("full")));
        Indexer.Write(atlas);
        return CoreCommands.Check(atlas) == 0 && failed == 0 ? 0 : 1;
    }
}
