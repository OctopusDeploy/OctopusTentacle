﻿using System.Diagnostics;
using System.Reflection;
using System.Text;
using Octopus.Client.Model;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Logging;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public class KubernetesAgentInstaller
{
    //This is the DNS of the localhost Kubernetes Server we add to the cluster in the KubernetesClusterInstaller.SetLocalhostRouting()
    const string LocalhostKubernetesServiceDns = "dockerhost.default.svc.cluster.local";
    
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

        var sb = new StringBuilder();
        var sprLogger = new LoggerConfiguration()
            .WriteTo.Logger(logger)
            .WriteTo.StringBuilder(sb)
            .MinimumLevel.Debug()
            .CreateLogger();

        var exitCode = SilentProcessRunner.ExecuteCommand(
            helmExePath,
            arguments,
            temporaryDirectory.DirectoryPath,
            sprLogger.Debug,
            sprLogger.Information,
            sprLogger.Error,
            CancellationToken.None);

        sw.Stop();

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to install Kubernetes Agent via Helm. Logs: {sb}");
        }

        isAgentInstalled = true;

        var thumbprint = await GetAgentThumbprint();

        logger.Information("Agent certificate thumbprint: {Thumbprint:l}", thumbprint);

        return thumbprint;
    }

    async Task<string> WriteValuesFile(int listeningPort)
    {
        using var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStreamFromPartialName("agent-values.yaml"));

        var valuesFile = await reader.ReadToEndAsync();

        var serverCommsAddress = $"https://{LocalhostKubernetesServiceDns}:{listeningPort}";

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
            .Replace("#{ConfigMapData}", configMapData);

        var valuesFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "agent-values.yaml");
        await File.WriteAllTextAsync(valuesFilePath, valuesFile, Encoding.UTF8);

        return valuesFilePath;
    }

    string BuildAgentInstallArguments(string valuesFilePath)
    {
        var args = new[]
        {
            "upgrade",
            "--install",
            "--atomic",
            $"-f \"{valuesFilePath}\"",
            GetImageAndRepository(),
            "--create-namespace",
            NamespaceFlag,
            KubeConfigFlag,
            AgentName,
            "oci://registry-1.docker.io/octopusdeploy/kubernetes-agent"
        };

        return string.Join(" ", args.WhereNotNull());
    }

    static string? GetImageAndRepository()
    {
        if (TeamCityDetection.IsRunningInTeamCity())
        {
            var tag = Environment.GetEnvironmentVariable("KubernetesAgentTests_ImageTag");
            return $"--set image.repository=\"docker.packages.octopushq.com/octopusdeploy/kubernetes-tentacle\" --set image.tag=\"{tag}\"";
        }

        return null;
    }

    async Task<string> GetAgentThumbprint()
    {
        string? thumbprint = null;
        var sb = new StringBuilder();
        var sprLogger = new LoggerConfiguration()
            .WriteTo.Logger(logger)
            .WriteTo.StringBuilder(sb)
            .MinimumLevel.Debug()
            .CreateLogger();

        var attempt = 0;
        do
        {
            var exitCode = SilentProcessRunner.ExecuteCommand(
                kubeCtlExePath,
                //get the generated thumbprint from the config map
                $"get cm tentacle-config --namespace {Namespace} --kubeconfig=\"{kubeConfigPath}\" -o \"jsonpath={{.data['Tentacle\\.CertificateThumbprint']}}\"",
                temporaryDirectory.DirectoryPath,
                sprLogger.Debug,
                x =>
                {
                    sprLogger.Information(x);
                    thumbprint = x;
                },
                sprLogger.Error,
                CancellationToken.None);
            
            if (exitCode != 0)
            {
                logger.Error("Failed to load thumbprint. Exit code {ExitCode}", exitCode);
                throw new InvalidOperationException($"Failed to load thumbprint. ExitCode: {exitCode}, Logs: {sb}");
            }

            if (thumbprint is not null)
            {
                return thumbprint;
            }

            if (attempt == 5)
            {
                break;
            }

            attempt++;
            await Task.Delay(500);
        } while (thumbprint is null);

        throw new InvalidOperationException("Failed to load the generated thumbprint after 5 attempts");
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