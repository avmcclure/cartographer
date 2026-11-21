namespace Cartographer.Tests;

public class SessionTests
{
    /// <summary>A stand-in for the real extractors: every "x.proj" is a component; its lines are "provides NAME" and "uses NAME".</summary>
    sealed class ProjExtractor : IExtractor
    {
        public int Order => 10;

        public void Extract(Extraction x)
        {
            foreach (var file in x.Repo.WithExtension(".proj"))
            {
                var c = x.Component(Path.GetDirectoryName(file)!.Replace('\\', '/'), Path.GetFileNameWithoutExtension(file), "library");
                foreach (var words in x.Repo.Read(file).Split('\n').Select(l => l.Trim().Split(' ')))
                {
                    if (words[0] == "provides") (c.Provides ??= []).Add(new Provided { Name = words[1], As = "nuget", Basis = Basis.Extracted, Source = x.Cite(c, "proj", file) });
                    if (words[0] == "uses") Extraction.AddLink(c, new Link { Type = "depends-on", Name = words[1], Basis = Basis.Extracted, Source = x.Cite(c, "proj", file) });
                }
            }
        }
    }

    static readonly IExtractor[] Extractors = [new GenericExtractor(), new ProjExtractor()];

    static string Git(string dir, params string[] args) => Shell.Git(dir, null, args).Trim();

    /// <summary>A new git repository in a temp directory holding the given files, committed. Returns its directory.</summary>
    static string NewRepository(params (string Path, string Text)[] files)
    {
        var dir = Directory.CreateTempSubdirectory("session-").FullName;
        Git(dir, "init", "-q", "-b", "main");
        Git(dir, "config", "user.name", "Test");
        Git(dir, "config", "user.email", "test@example.com");
        Git(dir, "config", "commit.gpgsign", "false");
        Commit(dir, files);
        return dir;
    }

    static void Commit(string dir, params (string Path, string Text)[] files)
    {
        foreach (var (path, text) in files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, path))!);
            File.WriteAllText(Path.Combine(dir, path), text);
        }
        Git(dir, "add", "-A");
        Git(dir, "commit", "-q", "-m", "Change");
    }

    /// <summary>An atlas committed on main, by default with a scan session started. Returns its directory.</summary>
    static string NewAtlas(bool session = true)
    {
        var dir = NewRepository((Atlas.ConfigFile, "organisation: Test\n"));
        if (session) Sessions.Start(dir);
        return dir;
    }

    static string Scan(string atlas, string folder, string checkout, bool full = false)
    {
        var loaded = Atlas.Load(atlas);
        var text = Scans.Run(loaded, folder, checkout, full, Extractors);
        Indexer.Write(loaded);
        return text;
    }

    static (int Code, string Text) Finish(string atlas, string folder) => Scans.Finish(Atlas.Load(atlas), folder);

    static string Section(string summary, string title)
    {
        var start = summary.IndexOf($"## {title}\n", StringComparison.Ordinal);
        if (start < 0) return "";
        var end = summary.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        return end < 0 ? summary[start..] : summary[start..end];
    }

    static string Shop() => NewRepository(
        ("lib/lib.proj", "provides Acme.Lib\n"), ("other/other.proj", "provides Acme.Other\n"), ("app/app.proj", "uses Acme.Lib\n"), ("README.md", "Shop\n"));

    [Fact]
    public void A_scan_and_a_rescan_each_end_in_one_commit_with_a_change_summary()
    {
        var atlas = NewAtlas();
        var shop = Shop();
        var first = Git(shop, "rev-parse", "HEAD");

        Scan(atlas, "shop", shop);
        var progress = ScanState.Load(Atlas.Load(atlas), "shop").InProgress!;
        Assert.Equal(("main", first, "full"), (progress.Branch, progress.Commit, progress.Mode));
        Assert.Equal(["app", "lib", "other"], progress.Components.Keys);
        Assert.All(progress.Components.Values, v => Assert.Equal("pending", v));

        var finished = Finish(atlas, "shop");
        Assert.True(finished.Code == 0, finished.Text);
        Assert.Equal("1", Git(atlas, "rev-list", "--count", "main..HEAD"));
        Assert.Equal("", Git(atlas, "status", "--porcelain"));
        Assert.StartsWith($"Scan shop at {first[..7]}\n\nParts: 4 added", Git(atlas, "log", "-1", "--format=%B"));
        var state = ScanState.Load(Atlas.Load(atlas), "shop");
        Assert.Null(state.InProgress);
        Assert.Equal((first, Fixtures.Date), (state.Default!.Value.State.ScannedCommit, state.Default.Value.State.Scanned));
        var summary = File.ReadAllText(Path.Combine(atlas, "changes", $"{Fixtures.Date}-shop-{first[..7]}.md"));
        Assert.StartsWith($"# Scan shop at {first[..7]}\n\nParts: 4 added, 0 removed, 0 retired. Links: 1 added, 0 removed.", summary);
        Assert.Contains("- repositories/shop/components/app (component)", Section(summary, "Parts added"));
        Assert.Contains("- repositories/shop/repository (repository)", Section(summary, "Parts added"));
        Assert.Empty(Scans.Due(Atlas.Load(atlas)));

        // The repository moves on; the lister (another area) would record the new tip like this.
        Commit(shop, ("app/app.proj", "uses Acme.Other\n"), ("README.md", "Shop, updated\n"));
        var second = Git(shop, "rev-parse", "HEAD");
        state.Locations = [new LocationState { Id = "github:acme/Shop", Primary = true, DefaultBranch = "main", Tip = second }];
        state.Save(Atlas.Load(atlas));

        var due = Assert.Single(Scans.Due(Atlas.Load(atlas)));
        Assert.Equal("shop", due.Folder);
        Assert.Contains("new commits", Assert.Single(due.Reasons));

        var changed = Scans.Changed(Atlas.Load(atlas), "shop", shop, Extractors);
        Assert.Contains("repositories/shop/components/app\n  app/app.proj\n", changed);
        Assert.Contains("outside any component\n  README.md\n", changed);
        Assert.DoesNotContain("components/lib", changed);

        Scan(atlas, "shop", shop);
        progress = ScanState.Load(Atlas.Load(atlas), "shop").InProgress!;
        Assert.Equal("changed", progress.Mode);
        Assert.Equal(new SortedDictionary<string, string> { ["app"] = "pending", ["lib"] = "done", ["other"] = "done" }, progress.Components);
        Assert.Equal("github:acme/Shop", Assert.Single(Atlas.Load(atlas).Find("repositories/shop/repository")!.Locations!).Id);

        finished = Finish(atlas, "shop");
        Assert.True(finished.Code == 0, finished.Text);
        Assert.Equal("2", Git(atlas, "rev-list", "--count", "main..HEAD"));
        Assert.Equal("", Git(atlas, "status", "--porcelain"));
        summary = File.ReadAllText(Path.Combine(atlas, "changes", $"{Fixtures.Date}-shop-{second[..7]}.md"));
        Assert.Contains("Parts: 0 added, 0 removed, 0 retired. Links: 1 added, 1 removed.", summary);
        Assert.Contains("- repositories/shop/components/app: depends-on repositories/shop/components/other (extracted)", Section(summary, "Links added"));
        Assert.Contains("- repositories/shop/components/app: depends-on repositories/shop/components/lib (extracted)", Section(summary, "Links removed"));

        var body = Sessions.PullRequestBody(atlas);
        Assert.StartsWith("2 repository scan(s)", body);
        Assert.InRange(body.IndexOf($"# Scan shop at {second[..7]}"), 0, body.IndexOf($"# Scan shop at {first[..7]}"));
        Assert.Contains("2 commit(s) beyond main", Sessions.End(atlas, false));
    }

    [Fact]
    public void A_scan_of_the_same_commit_keeps_the_components_already_done()
    {
        var atlas = NewAtlas();
        var shop = Shop();
        Scan(atlas, "shop", shop);
        var state = ScanState.Load(Atlas.Load(atlas), "shop");
        state.InProgress!.Components["lib"] = "done";
        state.Save(Atlas.Load(atlas));

        Scan(atlas, "shop", shop);
        Assert.Equal(new SortedDictionary<string, string> { ["app"] = "pending", ["lib"] = "done", ["other"] = "pending" }, ScanState.Load(Atlas.Load(atlas), "shop").InProgress!.Components);
        Scan(atlas, "shop", shop, full: true);
        Assert.All(ScanState.Load(Atlas.Load(atlas), "shop").InProgress!.Components.Values, v => Assert.Equal("pending", v));
    }

    [Fact]
    public void Finish_refuses_while_an_entry_of_the_repository_is_invalid()
    {
        var atlas = NewAtlas();
        Scan(atlas, "shop", Shop());
        var loaded = Atlas.Load(atlas);
        loaded.Find("repositories/shop/components/app")!.Summary = "";
        loaded.Save();

        var (code, text) = Finish(atlas, "shop");
        Assert.Equal(1, code);
        Assert.Contains("error: repositories/shop/components/app: summary is required", text);
        Assert.NotNull(ScanState.Load(Atlas.Load(atlas), "shop").InProgress);
        Assert.Equal("0", Git(atlas, "rev-list", "--count", "main..HEAD"));
    }

    [Fact]
    public void Finish_refuses_to_commit_on_main()
    {
        var atlas = NewAtlas(session: false);
        Scan(atlas, "shop", Shop());
        Assert.Contains("session start", Assert.Throws<UsageException>(() => Finish(atlas, "shop")).Message);
        Assert.Equal("1", Git(atlas, "rev-list", "--count", "HEAD"));
    }

    [Fact]
    public void Finish_leaves_another_scan_in_progress_out_of_the_commit()
    {
        var atlas = NewAtlas();
        Scan(atlas, "shop", Shop());
        Scan(atlas, "tools", NewRepository(("tool/tool.proj", "provides Acme.Tool\n")));
        File.WriteAllText(Path.Combine(atlas, "notes.txt"), "a person's own file\n");

        Assert.Equal(0, Finish(atlas, "shop").Code);
        var left = Git(atlas, "status", "--porcelain", "--untracked-files=all").Split('\n');
        Assert.Contains("?? notes.txt", left);
        Assert.All(left.Where(l => l != "?? notes.txt"), l => Assert.StartsWith("?? repositories/tools/", l));
        Assert.DoesNotContain("repositories/tools/", Git(atlas, "show", "--name-only", "--format=", "HEAD").Replace("_generated/repositories/tools/", ""));

        File.Delete(Path.Combine(atlas, "notes.txt"));
        Assert.Equal(0, Finish(atlas, "tools").Code);
        Assert.Equal("", Git(atlas, "status", "--porcelain"));
    }

    [Fact]
    public void An_interrupted_scan_is_due_first_then_what_was_never_scanned()
    {
        var atlas = NewAtlas();
        Scan(atlas, "shop", Shop());
        new ScanState { Repository = Ids.Repository("aaa") }.Save(Atlas.Load(atlas));

        var due = Scans.Due(Atlas.Load(atlas));
        Assert.Equal(["shop", "aaa"], due.Select(d => d.Folder));
        Assert.Contains("not finished", due[0].Reasons[0]);
        Assert.Equal(["never scanned"], due[1].Reasons);
    }

    [Fact]
    public void Question_retry_finds_a_name_now_provided_a_repository_that_changed_and_an_answer_a_person_typed()
    {
        var atlas = NewAtlas();
        Scan(atlas, "app", NewRepository(("app/app.proj", "provides Acme.App\n")));
        var loaded = Atlas.Load(atlas);
        QuestionList.Update(loaded.Find("repositories/app/components/app")!, list =>
        {
            list.Add("Which component provides Acme.Late.", "name Acme.Late", "A component providing it.", "agent");
            list.Add("What the lib repository is for.", "repository repositories/lib/repository", "Its purpose, once scanned.", "agent");
            list.Add("Who owns this.", "person", "A team name.", "person")!.Answer = "The billing team.";
        });
        loaded.Save();
        Assert.Equal(0, Finish(atlas, "app").Code);
        Assert.Equal(["q3"], QuestionRetry.Find(Atlas.Load(atlas)).Select(i => i.Question));

        Scan(atlas, "lib", NewRepository(("lib/lib.proj", "provides Acme.Late\n")));
        Assert.Equal(0, Finish(atlas, "lib").Code);

        var retry = QuestionRetry.Find(Atlas.Load(atlas));
        Assert.Equal(["q3", "q1", "q2"], retry.Select(i => i.Question));
        Assert.All(retry, i => Assert.Equal("repositories/app/components/app", i.Entry));
        Assert.Equal("a person answered; fold it in", retry[0].Reason);
        Assert.Equal("Acme.Late is now provided by repositories/lib/components/lib", retry[1].Reason);
        Assert.StartsWith("repository lib has changed", retry[2].Reason);

        var due = Assert.Single(Scans.Due(Atlas.Load(atlas)));
        Assert.Equal(("app", "3 open question(s) can be retried"), (due.Folder, Assert.Single(due.Reasons)));
    }

    [Fact]
    public void Session_start_refuses_a_dirty_atlas_and_numbers_a_branch_name_already_taken()
    {
        var atlas = NewAtlas(session: false);
        File.WriteAllText(Path.Combine(atlas, "stray.md"), "uncommitted\n");
        Assert.Contains("uncommitted changes", Assert.Throws<UsageException>(() => Sessions.Start(atlas)).Message);
        Assert.Equal("main", Sessions.Branch(atlas));

        File.Delete(Path.Combine(atlas, "stray.md"));
        Assert.Equal($"scan/{Fixtures.Date}", Sessions.Start(atlas));
        Assert.Equal($"scan/{Fixtures.Date}-2", Sessions.Start(atlas));
        Assert.Equal($"scan/{Fixtures.Date}-2", Sessions.Branch(atlas));
    }

    [Fact]
    public void A_pull_request_needs_a_remote()
    {
        var atlas = NewAtlas();
        Assert.Contains("git remote add origin", Assert.Throws<UsageException>(() => Sessions.End(atlas, true)).Message);
    }
}
