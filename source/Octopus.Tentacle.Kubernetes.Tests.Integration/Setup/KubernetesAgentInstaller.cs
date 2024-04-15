using System.Diagnostics;
using System.Reflection;
using System.Text;
using Octopus.Client.Model;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Security.Certificates;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public class KubernetesAgentInstaller
{
    readonly string helmExePath;
    readonly string kubeCtlExePath;
    readonly TemporaryDirectory temporaryDirectory;
    readonly ILogger logger;
    readonly string kubeConfigPath;
    
    bool isAgentInstalled;

    public KubernetesAgentInstaller(TemporaryDirectory temporaryDirectory, string helmExePath, string kubeCtlExePath, string kubeConfigPath, ILogger logger)
    {
        this.temporaryDirectory = temporaryDirectory;
        this.helmExePath = helmExePath;
        this.kubeCtlExePath = kubeCtlExePath;
        this.kubeConfigPath = kubeConfigPath;
        this.logger = logger;

        AgentName = Guid.NewGuid().ToString("N");
    }

    public string AgentName { get; }

    string Namespace => $"octopus-agent-{AgentName}";
    
    public Uri SubscriptionId { get; } = PollingSubscriptionId.Generate();

    public async Task<string> InstallAgent(int listeningPort)
    {
        var valuesFilePath = await WriteValuesFile(listeningPort);
        var arguments = BuildAgentInstallArguments(valuesFilePath);

        var sw = new Stopwatch();
        sw.Restart();
        var exitCode = SilentProcessRunner.ExecuteCommand(
            helmExePath,
            arguments,
            temporaryDirectory.DirectoryPath,
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

        var thumbprint = GetAgentThumbprint();
        
        logger.Information("Agent certificate thumbprint: {Thumbprint:l}", thumbprint);

        return thumbprint;
    }

    async Task<string> WriteValuesFile(int listeningPort)
    {
        var asm = Assembly.GetExecutingAssembly();
        var valuesFileName = asm.GetManifestResourceNames().First(n => n.Contains("agent-values.yaml", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(asm.GetManifestResourceStream(valuesFileName)!);

        var valuesFile = await reader.ReadToEndAsync();

        var serverCommsAddress = $"https://dockerhost.default.svc.cluster.local:{listeningPort}";

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
            .Replace("#{ConfigMapData}", configMapData);

        var valuesFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "agent-values.yaml");
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
            "--version \"0.7.1-ap-inject-test-data-20240412040646\"",
            "oci://docker.packages.octopushq.com/kubernetes-agent"
            //"oci://registry-1.docker.io/octopusdeploy/kubernetes-agent"
        );
    }

    string GetAgentThumbprint()
    {
        string? thumbprint = null;
        var exitCode = SilentProcessRunner.ExecuteCommand(
            kubeCtlExePath,
            //get the generated thumbprint from the config map
            $"get cm tentacle-config --namespace {Namespace} --kubeconfig=\"{kubeConfigPath}\" -o \"jsonpath={{.data['Tentacle\\.CertificateThumbprint']}}\"",
            temporaryDirectory.DirectoryPath,
            logger.Debug,
            x => thumbprint = x,
            logger.Error,
            CancellationToken.None);
        
        if (exitCode != 0 || thumbprint is null)
        {
            logger.Error("Failed to load thumbprint");
            throw new InvalidOperationException($"Failed to load thumbprint");
        }

        return thumbprint;
    }

    string NamespaceFlag => $"--namespace \"{Namespace}\"";
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
                helmExePath,
                uninstallArgs,
                temporaryDirectory.DirectoryPath,
                logger.Debug,
                logger.Information,
                logger.Error,
                CancellationToken.None);

            if (exitCode != 0)
            {
                logger.Error("Failed to uninstall Kubernetes Agent {AgentName} via Helm", AgentName);
            }
        }
    }
}