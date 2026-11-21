using System.ComponentModel;
using System.Diagnostics;

namespace Cartographer;

/// <summary>Runs git and gh where the error text or standard input matters; Checkout.Git covers plain queries. Never prompts.</summary>
public static class Shell
{
    public static (int Code, string Out, string Error) Run(string directory, string program, string? input, params string[] args)
    {
        var info = new ProcessStartInfo(program) { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment["GH_PROMPT_DISABLED"] = "1";
        foreach (var a in args) info.ArgumentList.Add(a);
        Process p;
        try { p = Process.Start(info)!; }
        catch (Win32Exception) { throw new UsageException($"'{program}' was not found on the PATH"); }
        using (p)
        {
            var error = p.StandardError.ReadToEndAsync();
            var output = p.StandardOutput.ReadToEndAsync();
            if (input is not null) p.StandardInput.Write(input);
            p.StandardInput.Close();
            p.WaitForExit();
            return (p.ExitCode, output.Result, error.Result);
        }
    }

    /// <summary>Runs git and returns its output; a failure is a UsageException carrying git's own message.</summary>
    public static string Git(string directory, string? input, params string[] args)
    {
        var (code, output, error) = Run(directory, "git", input, args);
        return code == 0 ? output : throw new UsageException($"git {string.Join(' ', args.Take(2))} failed in {directory}: {error.Trim()}");
    }
}
