using System.Diagnostics;

namespace Cartographer;

public static partial class Clones
{
    /// <summary>
    /// Fetches or updates the cached partial clone of a repository's primary location, checked out at its default
    /// branch tip, and returns the directory. <paramref name="repositoryFolder"/> is the atlas folder name.
    /// </summary>
    public static string Ensure(Atlas atlas, string repositoryFolder)
    {
        var primary = ScanState.Load(atlas, repositoryFolder).Primary
            ?? throw new UsageException($"'{repositoryFolder}' has no known location. Run: cartographer repos list --refresh");
        var url = Scm.CloneUrl(primary.Id);
        var parent = Path.Combine(CacheRoot, Ids.Slug(atlas.Config.Organisation));
        var dir = Path.Combine(parent, repositoryFolder);
        if (Directory.Exists(Path.Combine(dir, ".git")))
        {
            Git(dir, url, "remote", "set-url", "origin", url);   // the primary may have moved to GitHub since the clone was made
            Git(dir, url, "fetch", "--quiet", "--prune", "origin");
        }
        else
        {
            Directory.CreateDirectory(parent);
            Git(parent, url, "clone", "--quiet", "--filter=blob:none", "--no-checkout", url, dir);
        }
        Git(dir, url, "checkout", "--quiet", "--force", "--detach", primary.DefaultBranch is { } branch ? $"origin/{branch}" : "origin/HEAD");
        return dir;
    }

    /// <summary>Deletes the whole clone cache.</summary>
    public static void Clear()
    {
        if (!Directory.Exists(CacheRoot)) return;
        // Git marks pack files read-only, which stops a delete on Windows.
        foreach (var file in Directory.EnumerateFiles(CacheRoot, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(CacheRoot, true);
    }

    /// <summary>Runs git with credentials left to its own helper and prompting off; a failure names the host and what to check.</summary>
    static void Git(string directory, string url, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var a in args) info.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(info)!;
            var output = p.StandardOutput.ReadToEndAsync();
            var error = p.StandardError.ReadToEnd().Trim();
            p.WaitForExit();
            _ = output.Result;
            if (p.ExitCode != 0)
                throw new UsageException($"git {args[0]} failed for {url}: {error}\nCheck that {new Uri(url).Host} can be reached and that git's credential helper holds a sign-in for it (git is not allowed to prompt).");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new UsageException("git is not installed or not on PATH; cloning needs it.");
        }
    }
}
