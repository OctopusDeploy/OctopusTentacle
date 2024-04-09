using System.Diagnostics;
using System.Reflection;
using System.Text;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public class KubernetesAgentInstaller
{
    readonly TemporaryDirectory tempDir;
    readonly ILogger logger;
    string? helmPath;
    string? kubeConfigPath;
    bool isAgentInstalled;

    public KubernetesAgentInstaller()
    {
        tempDir = new TemporaryDirectory();

        logger = new LoggerConfiguration()
            .WriteTo.NUnitOutput()
            .WriteTo.Console()
            .WriteTo.File("w:\\temp\\agent-install.log")
            .CreateLogger()
            .ForContext<KubernetesClusterInstaller>();

        AgentName = Guid.NewGuid().ToString("N");
    }

    public string AgentName { get; }

    public async Task DownloadHelm()
    {
        //download helm
        var helmDownloader = new HelmDownloader(logger);
        helmPath = await helmDownloader.Download(tempDir.DirectoryPath, CancellationToken.None);
    }

    public async Task InstallAgent(string kubeConfigPath)
    {
        if (helmPath is null)
            throw new InvalidOperationException("Helm has not been downloaded");

        this.kubeConfigPath = kubeConfigPath;

        var valuesFilePath = await WriteValuesFile();
        var arguments = BuildAgentInstallArguments(valuesFilePath);

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

    async Task<string> WriteValuesFile()
    {
        var asm = Assembly.GetExecutingAssembly();
        var valuesFileName = asm.GetManifestResourceNames().First(n => n.Contains("agent-values.yaml", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(asm.GetManifestResourceStream(valuesFileName)!);

        var valuesFile = await reader.ReadToEndAsync();

        var configMapData = @"
        Octopus.Home: /octopus
        Tentacle.Deployment.ApplicationDirectory: /octopus/Applications
        Tentacle.Services.IsRegistered: 'true'
        Tentacle.Services.NoListen: 'true'";

        valuesFile = valuesFile.Replace("#{TargetName}", AgentName)
            .Replace("#{ServerCommsAddress}", $"https://localhost:{123456}")
            .Replace("#{ServerUrl}", "https://octopus.internal/")
            .Replace("#{ConfigMapData}", configMapData);

        var valuesFilePath = Path.Combine(tempDir.DirectoryPath, "agent-values.yaml");
        await File.WriteAllTextAsync(valuesFilePath, valuesFile, Encoding.UTF8);

        return valuesFilePath;
    }

    string BuildAgentInstallArguments(string valuesFilePath)
    {
        return string.Join(" ",
            "upgrade",
            "--install",
            "--atomic",
            $"-f \"{valuesFilePath}\"",
            "--create-namespace",
            "--debug",
            "-v 10",
            NamespaceFlag,
            KubeConfigFlag,
            AgentName,
            "oci://registry-1.docker.io/octopusdeploy/kubernetes-agent"
        );
    }

    string NamespaceFlag => $"--namespace \"octopus-agent-{AgentName}\"";
    string KubeConfigFlag => $"--kubeconfig \"{kubeConfigPath}\"";

    public void Dispose()
    {
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