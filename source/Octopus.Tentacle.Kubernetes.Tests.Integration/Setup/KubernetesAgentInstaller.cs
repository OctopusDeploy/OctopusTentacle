using System.Diagnostics;
using System.Reflection;
using System.Text;
using Octopus.Client.Model;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;
using Octopus.Tentacle.Security.Certificates;
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
    
    public Uri SubscriptionId { get; } = PollingSubscriptionId.Generate();

    public async Task DownloadHelm()
    {
        //download helm
        var helmDownloader = new HelmDownloader(logger);
        helmPath = await helmDownloader.Download(tempDir.DirectoryPath, CancellationToken.None);
    }

    public async Task InstallAgent(string kubeConfigPath, int listeningPort)
    {
        if (helmPath is null)
            throw new InvalidOperationException("Helm has not been downloaded");

        this.kubeConfigPath = kubeConfigPath;

        var valuesFilePath = await WriteValuesFile(listeningPort);
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

    async Task<string> WriteValuesFile(int listeningPort)
    {
        var asm = Assembly.GetExecutingAssembly();
        var valuesFileName = asm.GetManifestResourceNames().First(n => n.Contains("agent-values.yaml", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(asm.GetManifestResourceStream(valuesFileName)!);

        var valuesFile = await reader.ReadToEndAsync();

        var serverCommsAddress = $"https://host.docker.internal:{listeningPort}";

        var configMapData = $@"
        Octopus.Home: /octopus
        Tentacle.Deployment.ApplicationDirectory: /octopus/Applications
        Tentacle.Communication.TrustedOctopusServers: >-
          [{{""Thumbprint"":""{TestCertificates.ServerPublicThumbprint}"",""CommunicationStyle"":{(int)CommunicationStyle.TentacleActive},""Address"":""{serverCommsAddress}"",""Squid"":null,""SubscriptionId"":""{SubscriptionId}""}}]
        Tentacle.Services.IsRegistered: 'true'
        Tentacle.Services.NoListen: 'true'";

        valuesFile = valuesFile
            .Replace("#{TargetName}", AgentName)
            .Replace("#{ServerCommsAddress}", serverCommsAddress)
            //this address is not needed because we don't need it to register itself
            .Replace("#{ServerUrl}", "https://octopus.internal/")
            .Replace("#{EncodedCertificate}", CertificateEncoder.ToBase64String(TestCertificates.Tentacle))
            .Replace("#{ConfigMapData}", configMapData)
            .Replace("#{ImageRepository}","docker.packages.octopushq.com/octopusdeploy/kubernetes-tentacle")
            .Replace("#{ImageTag}", "8.1.1320-pull-870");

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
            NamespaceFlag,
            KubeConfigFlag,
            AgentName,
            "--version \"0.7.1-ap-inject-test-data-20240410030830\"",
            "oci://docker.packages.octopushq.com/kubernetes-agent"
            //"oci://registry-1.docker.io/octopusdeploy/kubernetes-agent"
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