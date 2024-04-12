using Halibut;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Support.Logging;
using Octopus.Tentacle.Util;
using Octopus.TestPortForwarder;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public abstract class KubernetesAgentIntegrationTest
{
    readonly KubernetesAgentInstaller kubernetesAgentInstaller = new();
    TemporaryDirectory tempDir = null!;
    string kubeCtlExe = null!;
    protected ILogger Logger { get; private set; }

    protected HalibutRuntime ServerHalibutRuntime { get; private set; } = null!;

    protected TentacleClient TentacleClient { get; private set; } = null!;

    protected KubernetesAgentIntegrationTest() 
    {
        Logger = new SerilogLoggerBuilder().Build();
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await kubernetesAgentInstaller.DownloadHelm();

        tempDir = new TemporaryDirectory();
        var kubectlDownloader = new KubeCtlDownloader(Logger);
        kubeCtlExe = await kubectlDownloader.Download(tempDir.DirectoryPath, CancellationToken.None);
        
        //create a new server halibut runtime
        var listeningPort = BuildServerHalibutRuntimeAndListen();
        
        await kubernetesAgentInstaller.InstallAgent(TestKubernetesCluster.KubeConfigPath, listeningPort);

        //kubectl get config map thumbprint value of the generated cert
        var thumbPrint = GetAgentThumbprint();
        
        //trust the generated cert thumbprint
        ServerHalibutRuntime.Trust(thumbPrint);

        BuildTentacleClient();
    }

    string GetAgentThumbprint()
    {
        string? thumbprint = null;
        var exitCode = SilentProcessRunner.ExecuteCommand(
            kubeCtlExe,
            //we give the cluster a unique name
            $"get cm tentacle-config --namespace octopus-agent-{kubernetesAgentInstaller.Namespace} --kubeconfig=\"{TestKubernetesCluster.KubeConfigPath}\" -o \"jsonpath={{.data['Tentacle\\.CertificateThumbprint']}}\"",
            tempDir.DirectoryPath,
            Logger.Debug,
            x => thumbprint = x,
            Logger.Error,
            CancellationToken.None);
        
        if (exitCode != 0 || thumbprint is null)
        {
            Logger.Error("Failed to load thumbprint");
            throw new InvalidOperationException($"Failed to load thumbprint");
        }

        return thumbprint;
    }

    [SetUp]
    public void SetUp()
    {
        Logger = new SerilogLoggerBuilder().Build();
    }

    void BuildTentacleClient()
    {
        var endpoint = new ServiceEndPoint(kubernetesAgentInstaller.SubscriptionId, TestCertificates.TentaclePublicThumbprint, ServerHalibutRuntime.TimeoutsAndLimits);
        
        var retrySettings = new RpcRetrySettings(true, TimeSpan.FromMinutes(2));
        var clientOptions = new TentacleClientOptions(retrySettings);
        
        TentacleClient.CacheServiceWasNotFoundResponseMessages(ServerHalibutRuntime);

        TentacleClient = new TentacleClient(
            endpoint,
            ServerHalibutRuntime,
            new PollingTentacleScriptObserverBackoffStrategy(),
            new NoTentacleClientObserver(),
            clientOptions);
    }

    int BuildServerHalibutRuntimeAndListen()
    {
        var serverHalibutRuntimeBuilder = new HalibutRuntimeBuilder()
            .WithServerCertificate(TestCertificates.Server)
            .WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits.RecommendedValues())
            .WithLogFactory(new TestContextLogCreator("Server", LogLevel.Trace).ToCachingLogFactory());

        ServerHalibutRuntime = serverHalibutRuntimeBuilder.Build();

        return ServerHalibutRuntime.Listen();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await ServerHalibutRuntime.DisposeAsync();
        kubernetesAgentInstaller?.Dispose();
    }
}