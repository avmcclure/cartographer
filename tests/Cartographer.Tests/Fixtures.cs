using System.Runtime.CompilerServices;

namespace Cartographer.Tests;

/// <summary>
/// Fixtures live in /fixtures/&lt;fixture&gt;/. Each sub-folder except "expected" is one repository's files; the folder
/// name is the repository's atlas folder name. An optional cartographer.yml beside them is the atlas config.
/// "expected" holds the whole atlas the mechanical pass must produce, compared byte for byte.
/// Run with CARTOGRAPHER_ACCEPT=1 to rewrite "expected", then review the diff.
/// </summary>
public static class Fixtures
{
    public const string Date = "2026-01-01";

    [ModuleInitializer]
    public static void FixTheDate() => Environment.SetEnvironmentVariable("CARTOGRAPHER_DATE", Date);

    public static string Root([CallerFilePath] string here = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "fixtures"));

    /// <summary>Copies one fixture repository to a temp directory and commits it, with a fixed author and date so the commit hash repeats.</summary>
    public static Checkout Checkout(string fixture, string repository)
    {
        var source = Path.Combine(Root(), fixture, repository);
        var target = Path.Combine(Directory.CreateTempSubdirectory("fixture-").FullName, repository);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var to = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to);
        }
        Git(target, "init", "-q", "-b", "main");
        Git(target, "add", "-A");
        Git(target, "-c", "user.name=Fixture", "-c", "user.email=fixture@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "Fixture");
        return Cartographer.Checkout.Open(repository, target);
    }

    static void Git(string dir, params string[] args)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var a in new[] { "-c", "core.autocrlf=false" }.Concat(args)) info.ArgumentList.Add(a);
        info.Environment["GIT_AUTHOR_DATE"] = info.Environment["GIT_COMMITTER_DATE"] = "2026-01-01T00:00:00Z";
        using var p = System.Diagnostics.Process.Start(info)!;
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', args)}: {error}");
    }

    /// <summary>Runs the mechanical pass over every repository of a fixture, in folder-name order, into a new atlas. Returns the saved atlas.</summary>
    public static Atlas Scan(string fixture)
    {
        var root = Directory.CreateTempSubdirectory("atlas-").FullName;
        var config = Path.Combine(Root(), fixture, Atlas.ConfigFile);
        File.WriteAllText(Path.Combine(root, Atlas.ConfigFile), File.Exists(config) ? File.ReadAllText(config) : "organisation: Fixture\n");
        var atlas = Atlas.Load(root);
        foreach (var repository in Directory.EnumerateDirectories(Path.Combine(Root(), fixture)).Select(d => Path.GetFileName(d)!).Where(n => n != "expected").Order(StringComparer.Ordinal))
            Scanner.Run(atlas, Checkout(fixture, repository));
        atlas.Save();
        Indexer.Write(atlas);
        return atlas;
    }

    /// <summary>Compares every file of the atlas with fixtures/&lt;fixture&gt;/expected, or rewrites expected when CARTOGRAPHER_ACCEPT=1.</summary>
    public static void AssertMatchesExpected(Atlas atlas, string fixture)
    {
        var expected = Path.Combine(Root(), fixture, "expected");
        static SortedDictionary<string, string> Read(string dir) => !Directory.Exists(dir) ? [] : new(Directory
            .EnumerateFiles(dir, "*", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            .ToDictionary(f => Path.GetRelativePath(dir, f).Replace('\\', '/'), File.ReadAllText), StringComparer.Ordinal);
        var actual = Read(atlas.Root);
        if (Environment.GetEnvironmentVariable("CARTOGRAPHER_ACCEPT") == "1")
        {
            if (Directory.Exists(expected)) Directory.Delete(expected, true);
            foreach (var (path, text) in actual)
            {
                var to = Path.Combine(expected, path);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.WriteAllText(to, text);
            }
            return;
        }
        var want = Read(expected);
        Assert.Equal(want.Keys, actual.Keys);
        foreach (var (path, text) in want) Assert.True(text == actual[path], $"{fixture}: {path} differs from expected.\n--- expected\n{text}\n--- actual\n{actual[path]}");
        Assert.DoesNotContain(Validator.Check(atlas), i => i.Error);
    }
}
