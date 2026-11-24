using System.ComponentModel;
using System.Diagnostics;

namespace Cartographer.Cli;

/// <summary>The site: its data and its build.</summary>
public sealed class SiteCommands : ICommands
{
    public void Register(Cli cli)
    {
        cli.Add("site prepare", "site prepare --out <dir> [--edit-url <url>]", "Write the data the site is built from.", a => Prepare(a.Atlas(), a.Required("out"), a.Option("edit-url")));
        cli.Add("site build", "site build --out <dir> [--edit-url <url>] [--base <path>]", "Run site prepare, then build the static site. Needs Node.js.", Build);
    }

    public static int Prepare(Atlas atlas, string outDir, string? editUrl)
    {
        var g = SitePrepare.Run(atlas, Path.GetFullPath(outDir), editUrl);
        Console.WriteLine($"site data written to {Path.GetFullPath(outDir)}: {g.Clusters.Count} clusters");
        if (g.Suggestion is { } s) Console.WriteLine(ClusterCommands.Describe(g, s));
        return 0;
    }

    static int Build(Args a)
    {
        var atlas = a.Atlas();
        var outDir = Path.GetFullPath(a.Required("out"));
        var template = Path.Combine(AppContext.BaseDirectory, "site-template");
        if (!File.Exists(Path.Combine(template, "package.json"))) throw new UsageException($"the site template is missing from this installation ({template})");
        try { Run("node", template, "--version"); }
        catch (Win32Exception) { throw new UsageException("Node.js was not found on PATH. `site build` needs Node.js 22 or later and npm; install it from https://nodejs.org and run again.\n`site prepare` needs no Node.js."); }

        // The site is built in a working copy under the cache, so the installed tool's own folder is never written to.
        var work = Path.Combine(Clones.CacheRoot, "site");
        foreach (var file in Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(work, Path.GetRelativePath(template, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to, true);
        }
        var prepared = Prepare(atlas, Path.Combine(work, "data"), a.Option("edit-url"));

        var npm = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
        var (stamp, wanted) = (Path.Combine(work, "node_modules", ".cartographer-lock"), File.ReadAllText(Path.Combine(work, "package-lock.json")));
        if (!File.Exists(stamp) || File.ReadAllText(stamp) != wanted)
        {
            if (Run(npm, work, "ci", "--no-audit", "--no-fund") != 0) return 1;
            File.WriteAllText(stamp, wanted);
        }
        var env = new Dictionary<string, string> { ["ASTRO_TELEMETRY_DISABLED"] = "1" };   // an atlas is internal: the build reports nothing to anyone
        if (a.Option("base") is { } b) env["CARTOGRAPHER_BASE"] = b;
        if (Run(npm, work, env, "run", "build", "--", "--outDir", outDir) != 0) return 1;
        Console.WriteLine($"site built at {outDir}");
        return prepared;
    }

    static int Run(string file, string dir, params string[] args) => Run(file, dir, null, args);

    static int Run(string file, string dir, Dictionary<string, string>? env, params string[] args)
    {
        // Windows will not start npm.cmd directly, so it runs through cmd.exe, as Credentials does for az.cmd.
        var viaCmd = OperatingSystem.IsWindows() && file.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
        var info = new ProcessStartInfo(viaCmd ? "cmd.exe" : file) { WorkingDirectory = dir, UseShellExecute = false };
        foreach (var arg in viaCmd ? ["/d", "/c", file, .. args] : args) info.ArgumentList.Add(arg);
        foreach (var (k, v) in env ?? []) info.Environment[k] = v;
        using var p = Process.Start(info)!;
        p.WaitForExit();
        return p.ExitCode;
    }
}
