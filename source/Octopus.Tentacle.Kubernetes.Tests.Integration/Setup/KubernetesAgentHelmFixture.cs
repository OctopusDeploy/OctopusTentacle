using System.Diagnostics;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tools;
using Octopus.Tentacle.Util;
using Xunit.Abstractions;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public class KubernetesAgentHelmFixture : IAsyncLifetime
{
    readonly TemporaryDirectory tempDir;
    readonly ILogger logger;
    string? helmPath;
    string? kubeConfigPath;
    bool isAgentInstalled;

    public KubernetesAgentHelmFixture(IMessageSink diagnosticMessageSink)
    {
        tempDir = new TemporaryDirectory();

        logger = new LoggerConfiguration()
            .WriteTo.TestOutput(diagnosticMessageSink)
            .WriteTo.Console()
            .WriteTo.File("w:\\temp\\agent-install.log")
            .CreateLogger()
            .ForContext<KubernetesClusterFixture>();

        AgentName = Guid.NewGuid().ToString("N");
    }

    public string AgentName { get; }

    public async Task InitializeAsync()
    {
        //download helm
        var helmDownloader = new HelmDownloader(logger);
        helmPath = await helmDownloader.Download(tempDir.DirectoryPath, CancellationToken.None);
    }

    public void InstallAgent(string kubeConfigPath)
    {
        if (helmPath is null)
            throw new InvalidOperationException("Helm has not been downloaded");

        this.kubeConfigPath = kubeConfigPath;

        var arguments = BuildAgentInstallArguments();

        var sw = new Stopwatch();
        sw.Restart();
        var exitCode = SilentProcessRunner.ExecuteCommand(
            helmPath,
            arguments,
            tempDir.DirectoryPath,
            logger.Debug,
            logger.Information,
            logger.Error,
            CancellationToken.None);

        sw.Stop();

        if (exitCode != 0)
        {
            throw new InvalidOperationException("Failed to install Kubernetes Agent via Helm");
        }

        isAgentInstalled = true;
    }

    string BuildAgentInstallArguments()
    {
        return string.Join(" ",
            "upgrade",
            "--install",
            "--atomic",
            "--set tentacle.ACCEPT_EULA=\"Y\"",
            $"--set tentacle.targetName=\"{AgentName}\"",
            //these addresses do not matter
            "--set tentacle.serverUrl=\"https://octopus.internal/\"",
            "--set tentacle.serverCommsAddress=\"https://polling.octopus.internal/\"",
            "--set tentacle.space=\"Default\"",
            "--set tentacle.targetEnvironments=\"{development}\"",
            "--set tentacle.targetRoles=\"{testing-cluster}\"",
            "--set tentacle.bearerToken=\"this-is-a-fake-bearer-token\"",
            "--create-namespace",
            NamespaceFlag,
            KubeConfigFlag,
            AgentName,
            "oci://registry-1.docker.io/octopusdeploy/kubernetes-agent"
        );
    }

    string NamespaceFlag => $"--namespace \"octopus-agent-{AgentName}\"";
    string KubeConfigFlag => $"--kubeconfig \"{kubeConfigPath}\"";

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;

        if (isAgentInstalled)
        {
            var uninstallArgs = string.Join(" ",
                "uninstall",
                KubeConfigFlag,
                NamespaceFlag,
                AgentName);

            var exitCode = SilentProcessRunner.ExecuteCommand(
                helmPath!,
                uninstallArgs,
                tempDir.DirectoryPath,
                logger.Debug,
                logger.Information,
                logger.Error,
                CancellationToken.None);

            if (exitCode != 0)
            {
                logger.Error("Failed to install Kubernetes Agent {AgentName} via Helm", AgentName);
            }
        }

        tempDir.Dispose();
    }
}