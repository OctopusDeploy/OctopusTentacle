using System;
using System.Text;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Logging;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Tooling;

public class KubeCtlTool
{
    readonly TemporaryDirectory temporaryDirectory;
    readonly string kubeCtlExePath;
    readonly string kubeConfigPath;
    readonly string ns;
    readonly ILogger logger;

    public KubeCtlTool(TemporaryDirectory temporaryDirectory, string kubeCtlExePath, string kubeConfigPath, string ns, ILogger logger)
    {
        this.temporaryDirectory = temporaryDirectory;
        this.kubeCtlExePath = kubeCtlExePath;
        this.kubeConfigPath = kubeConfigPath;
        this.ns = ns;
        this.logger = logger;
    }

    public Task<KubeCtlCommandResult> ExecuteNamespacedCommand(string command, CancellationToken cancellationToken = default)
    {
        return ExecuteCommand($"{command} --namespace {ns}", cancellationToken);
    }

    async Task<KubeCtlCommandResult> ExecuteCommand(string command, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var sprLogger = new LoggerConfiguration()
            .WriteTo.Logger(logger)
            .WriteTo.StringBuilder(sb)
            .MinimumLevel.Debug()
            .CreateLogger();

        var stdOut = new List<string>();
        var stdErr = new List<string>();

        var exitCode = await SilentProcessRunner.ExecuteCommandAsync(
            kubeCtlExePath,
            $"{command} --kubeconfig=\"{kubeConfigPath}\"",
            temporaryDirectory.DirectoryPath,
            sprLogger.Debug,
            x =>
            {
                sprLogger.Information(x);
                stdOut.Add(x);
            },
            y =>
            {
                sprLogger.Error(y);
                stdErr.Add(y);
            },
            cancel: cancellationToken,
            abandon: CancellationToken.None);

        return new (exitCode, stdOut, stdErr);
    }

    public record KubeCtlCommandResult(int ExitCode, IEnumerable<string> StdOut, IEnumerable<string> StdError);
}