using System.Diagnostics;

namespace Octopus.Pipelines.Runner.StepRunner;

/// <summary>
/// How a <c>run</c> script is executed, as GitHub Actions' <c>shell:</c> does: a command line in which <c>{0}</c> is
/// the script file's path. With no shell, bash if the image has it, otherwise sh.
/// </summary>
sealed record Shell(IReadOnlyList<string> CommandLine, string Extension)
{
    static readonly Shell Bash = new(["bash", "--noprofile", "--norc", "-eo", "pipefail", "{0}"], ".sh");
    static readonly Shell Sh = new(["sh", "-e", "{0}"], ".sh");

    public static Shell Resolve(string? shell, string? path) => shell?.Trim() switch
    {
        null or "" => OnPath("bash", path) ? Bash : Sh,
        "bash" => Bash,
        "sh" => Sh,
        "pwsh" => new(["pwsh", "-command", ". '{0}'"], ".ps1"),
        "python" => new(["python", "{0}"], ".py"),
        var custom when custom.Contains("{0}", StringComparison.Ordinal) =>
            new(custom.Split(' ', StringSplitOptions.RemoveEmptyEntries), ""),
        var other => throw new NotSupportedException(
            $"Unknown shell '{other}'. Use bash, sh, pwsh, python, or a command line with {{0}} where the script's path goes."),
    };

    public ProcessStartInfo StartInfo(string scriptPath)
    {
        var startInfo = new ProcessStartInfo(CommandLine[0]);
        foreach (var arg in CommandLine.Skip(1))
        {
            startInfo.ArgumentList.Add(arg.Replace("{0}", scriptPath, StringComparison.Ordinal));
        }

        return startInfo;
    }

    static bool OnPath(string program, string? path) =>
        (path ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries).Any(dir => File.Exists(Path.Combine(dir, program)));
}
