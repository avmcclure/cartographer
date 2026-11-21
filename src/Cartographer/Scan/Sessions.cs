namespace Cartographer;

/// <summary>A scan session in the atlas's git repository: its branch, and at the end a local review or a pull request. Never merges.</summary>
public static class Sessions
{
    /// <summary>About the most a pull request description holds; GitHub's limit is 65,536 characters.</summary>
    const int BodyLimit = 60_000;

    /// <summary>The branch the atlas is on.</summary>
    public static string Branch(string root) => Checkout.Git(root, "symbolic-ref", "--short", "HEAD")?.Trim()
        ?? throw new UsageException($"{root} is not a git repository on a branch. Run: git init, then cartographer session start");

    static bool Exists(string root, string reference) => Checkout.Git(root, "rev-parse", "--verify", "--quiet", reference + "^{commit}") is not null;

    /// <summary>The atlas's default branch and a ref that has it (local, else the remote's copy), or null in a repository with neither main nor master.</summary>
    static (string Name, string Ref)? Default(string root)
    {
        foreach (var name in new[] { "main", "master" })
            foreach (var reference in new[] { name, "origin/" + name })
                if (Exists(root, reference)) return (name, reference);
        return null;
    }

    /// <summary>Creates the scan branch and switches to it. Returns its name.</summary>
    public static string Start(string root, string? branch = null)
    {
        Branch(root);
        if (Shell.Git(root, null, "status", "--porcelain").Trim().Length > 0)
            throw new UsageException("the atlas has uncommitted changes; commit or discard them before starting a session (git status shows them)");
        var name = branch ?? $"scan/{Clock.Today}";
        for (var n = 2; branch is null && Exists(root, "refs/heads/" + name); n++) name = $"scan/{Clock.Today}-{n}";
        // A branch named explicitly that already exists is a session being continued.
        if (branch is not null && Exists(root, "refs/heads/" + name)) Shell.Git(root, null, "checkout", "-q", name);
        else Shell.Git(root, null, "checkout", "-q", "-b", name);
        return name;
    }

    /// <summary>The change summaries added on this branch, newest first, joined; those that do not fit are counted.</summary>
    /// <summary>Commits everything changed in the atlas on the scan branch: work that is not one repository's finished scan (a partial scan, answered questions, systems and tags).</summary>
    public static string Commit(string root, string message)
    {
        if (Branch(root) is "main" or "master") throw new UsageException("refusing to commit on the default branch; run: cartographer session start");
        if (Shell.Git(root, null, "status", "--porcelain").Trim().Length == 0) return "nothing to commit";
        Shell.Git(root, null, "add", "-A");
        Shell.Git(root, null, "commit", "-q", "-m", message);
        return $"committed on {Branch(root)}: {message}";
    }

    public static string PullRequestBody(string root)
    {
        var range = Default(root) is { } d ? $"{d.Ref}..HEAD" : "HEAD";
        var files = (Checkout.Git(root, "log", "--diff-filter=A", "--name-only", "--format=", range, "--", "changes/") ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Distinct().Where(f => File.Exists(Path.Combine(root, f))).ToList();
        var body = $"{files.Count} repository scan(s) on this branch.\n";
        for (var i = 0; i < files.Count; i++)
        {
            var summary = File.ReadAllText(Path.Combine(root, files[i])).Trim('\n');
            if (body.Length + summary.Length + 2 > BodyLimit) return body + $"\n{files.Count - i} older change summaries are left out; they are in changes/.\n";
            body += "\n" + summary + "\n";
        }
        return body;
    }

    /// <summary>Without a pull request: where the work is and how to review it. With one: pushes the branch and opens the pull request.</summary>
    public static string End(string root, bool pullRequest)
    {
        var branch = Branch(root);
        var main = Default(root);
        var commits = (Checkout.Git(root, "rev-list", "--count", main is null ? "HEAD" : $"{main.Value.Ref}..HEAD") ?? "0").Trim();
        if (!pullRequest)
            return $"{branch}: {commits} commit(s){(main is null ? "" : $" beyond {main.Value.Name}")}, kept local\n" +
                $"review: git -C {root} log --stat {(main is null ? "" : main.Value.Ref + "..")}{branch}\n" +
                $"        cartographer site build --atlas {root} --out <dir>\n" +
                "publish: cartographer session end --pull-request\n";

        if (main is null || branch == main.Value.Name) throw new UsageException($"a pull request needs a scan branch beside the default branch; this atlas is on {branch}. Run: cartographer session start");
        var remotes = Shell.Git(root, null, "remote").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (remotes.Length == 0) throw new UsageException("the atlas has no git remote to push to. Add one: git remote add origin <url>");
        if (commits == "0") throw new UsageException($"{branch} has no commits beyond {main.Value.Name}; there is nothing to publish");
        var remote = remotes.Contains("origin") ? "origin" : remotes[0];
        Shell.Git(root, null, "push", "-q", "-u", remote, branch);
        (int Code, string Out, string Error) created;
        try { created = Shell.Run(root, "gh", PullRequestBody(root), "pr", "create", "--base", main.Value.Name, "--head", branch, "--title", $"Atlas scan {branch}", "--body-file", "-"); }
        catch (UsageException) { throw new UsageException($"{branch} is pushed to {remote}, but the GitHub CLI 'gh' is not installed. Install it and sign in (gh auth login, or set GH_TOKEN), then run this again"); }
        if (created.Code != 0) throw new UsageException($"{branch} is pushed to {remote}, but gh pr create failed: {created.Error.Trim()}");
        return $"{branch}: {commits} commit(s) pushed to {remote}\n{created.Out.Trim()}\n";
    }
}
