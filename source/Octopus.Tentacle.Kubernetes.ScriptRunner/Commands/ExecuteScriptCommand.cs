using System.CommandLine;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.ScriptRunner.Commands;

public class ExecuteScriptCommand : RootCommand
{
    readonly IShell shell;

    public ExecuteScriptCommand()
        : base("Executes the script found in the work directory for the script ticket")
    {
        if (PlatformDetection.IsRunningOnWindows)
            shell = new PowerShell();
        else
            shell = new Bash();

        var scriptPathOption = new Option<string?>(
            name: "--script",
            description: "The path to the script file to execute");
        AddOption(scriptPathOption);

        var scriptArgsOption = new Option<string[]>(
            name: "--args",
            description: "The arguments to be passed to the script")
        {
            AllowMultipleArgumentsPerToken = true
        };
        AddOption(scriptArgsOption);

        var logToConsoleOption = new Option<bool>(
            name: "--logToConsole",
            description: "If true, also writes the script logs to the console");
        AddOption(logToConsoleOption);

        this.SetHandler(async context =>
        {
            var scriptPath = context.ParseResult.GetValueForOption(scriptPathOption);
            var scriptArgs = context.ParseResult.GetValueForOption(scriptArgsOption);
            var logToConsole = context.ParseResult.GetValueForOption(logToConsoleOption);
            var token = context.GetCancellationToken();
            var exitCode = await ExecuteScript(scriptPath!, scriptArgs,logToConsole, token);
            context.ExitCode = exitCode;
        });
    }

    async Task<int> ExecuteScript(string scriptPath, string[]? scriptArgs, bool logToConsole, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        // we get erroneously left "s sometimes in k8s, so strip these
        scriptPath = scriptPath.Trim('"');

        var workingDirectory = Path.GetDirectoryName(scriptPath);
        var scriptTicket = workingDirectory!.Split(Path.DirectorySeparatorChar).Last();

        var workspace = new BashScriptWorkspace(
            new ScriptTicket(scriptTicket),
            workingDirectory,
            new OctopusPhysicalFileSystem(new SystemLog()),
            new SensitiveValueMasker());

        var log = workspace.CreateLog();
        var logWriter = log.CreateWriter();

        using var writer = logToConsole
            ? new ConsoleLogWriterWrapper(logWriter)
            : logWriter;

        scriptArgs ??= Array.Empty<string>();

        try
        {
            var exitCode = SilentProcessRunner.ExecuteCommand(
                shell.GetFullPath(),
                shell.FormatCommandArguments(scriptPath, scriptArgs, false),
                workingDirectory,
                output => writer.WriteOutput(ProcessOutputSource.Debug, output),
                output => writer.WriteOutput(ProcessOutputSource.StdOut, output),
                output => writer.WriteOutput(ProcessOutputSource.StdErr, output),
                cancellationToken);

            return exitCode;
        }
        catch (Exception ex)
        {
            writer.WriteOutput(ProcessOutputSource.StdErr, "An exception was thrown when invoking " + shell.GetFullPath() + ": " + ex.Message);
            writer.WriteOutput(ProcessOutputSource.StdErr, ex.ToString());

            return ScriptExitCodes.PowershellInvocationErrorExitCode;
        }
    }

    class ConsoleLogWriterWrapper : IScriptLogWriter
    {
        readonly IScriptLogWriter writer;

        public ConsoleLogWriterWrapper(IScriptLogWriter writer)
        {
            this.writer = writer;
        }

        public void WriteOutput(ProcessOutputSource source, string message)
        {
            Console.WriteLine($"[{source}] {message}");
            writer.WriteOutput(source, message);
        }

        public void Dispose()
        {
            writer.Dispose();
        }
    }
}