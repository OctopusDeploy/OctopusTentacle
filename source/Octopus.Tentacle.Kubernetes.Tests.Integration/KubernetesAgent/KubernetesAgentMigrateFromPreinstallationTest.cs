using System.Text;
using FluentAssertions;
using k8s;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.KubernetesAgent;

public class KubernetesAgentMigrateFromPreinstallationTest
{
    
    readonly ISystemLog systemLog = new SystemLog();
    
    const string SourceConfigMapName = "tentacle-config-pre";
    const string SourceSecretName = "tentacle-secret-pre";
    const string DestinationConfigMapName = "tentacle-config";
    const string DestinationSecretName = "tentacle-secret";
    MigratePreInstalledKubernetesDeploymentTargetCommand commandToRun;
    KubernetesFileWrappedProvider kubernetesConfigClient;
    string commandNamespace;
    k8s.Kubernetes client;
    string[] commandArguments;

    [SetUp]
    public void Init()
    {
        kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        commandToRun = new MigratePreInstalledKubernetesDeploymentTargetCommand(new Lazy<IKubernetesClientConfigProvider>(kubernetesConfigClient), systemLog, new LogFileOnlyLogger());
        commandNamespace = Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, commandNamespace);
        client = new k8s.Kubernetes(kubernetesConfigClient.Get());
        commandArguments = [
            $"--source-config-map-name={SourceConfigMapName}", 
            $"--source-secret-name={SourceSecretName}",
            $"--destination-config-map-name={DestinationConfigMapName}",
            $"--destination-secret-name={DestinationSecretName}",
            $"--namespace={commandNamespace}"
        ];
    }
    
    [Test]
    public async Task MigrationFromPreinstallHook_ShouldCopyData()
    {
        //Arrange
        var validationKey = Guid.NewGuid().ToString("N");
        var sourceConfigMapData = new Dictionary<string, string>
        {
            {"validationKey", validationKey},
            {"Tentacle.Services.IsRegistered", "true"}
        };
        var sourceSecretData = new Dictionary<string, string>
        {
            {"validationKey", validationKey}
        };
        
        // Namespace
        await CreateCommandNamespace(commandNamespace);
        
        // Sources
        await CreateConfigmap(SourceConfigMapName, commandNamespace, sourceConfigMapData);
        await CreateSecret(SourceSecretName, commandNamespace, sourceSecretData);

        
        // Targets
        await CreateConfigmap(DestinationConfigMapName, commandNamespace);
        await CreateSecret(DestinationSecretName, commandNamespace);

        

        //Act
        commandToRun.Start(commandArguments, new NoninteractiveHost(), []);
        
        //Assert
        var configMap = await client.CoreV1.ReadNamespacedConfigMapAsync(DestinationConfigMapName, commandNamespace);
        var secret = await client.CoreV1.ReadNamespacedSecretAsync(DestinationSecretName, commandNamespace);

        configMap.Data.TryGetValue("validationKey", out var validationKeyFromKubernetesConfigMap);
        validationKeyFromKubernetesConfigMap.Should().Be(validationKey);
        
        secret.Data.TryGetValue("validationKey", out var validationKeyFromKubernetesSecret);
        validationKeyFromKubernetesSecret.Should().Equal(Encoding.UTF8.GetBytes(validationKey));
    }
    
    [Test]
    public async Task MigrationFromPreinstallHook_ShouldNotRunWhenTentacleIsAlreadyRegistered()
    {
        //Arrange
        var validationKey = Guid.NewGuid().ToString("N");
        var sourceConfigMapData = new Dictionary<string, string>
        {
            {"validationKey", "should-not-be-here"},
            {"Tentacle.Services.IsRegistered", "true"}
        };
        var sourceSecretData = new Dictionary<string, string>
        {
            {"validationKey", "should-not-be-here"}
        };
        var destinationConfigMapData = new Dictionary<string, string>
        {
            {"validationKey", validationKey},
            {"Tentacle.Services.IsRegistered", "true"}
        };
        var destinationSecretData = new Dictionary<string, string>
        {
            {"validationKey", validationKey}
        };
        
        // namespace
        await CreateCommandNamespace(commandNamespace);
        
        //Sources
        await CreateConfigmap(SourceConfigMapName, commandNamespace, sourceConfigMapData);
        await CreateSecret(SourceSecretName, commandNamespace, sourceSecretData);
        
        // Targets
        await CreateConfigmap(DestinationConfigMapName, commandNamespace, destinationConfigMapData);
        await CreateSecret(DestinationSecretName, commandNamespace, destinationSecretData);

        
        //Act
        commandToRun.Start(commandArguments, new NoninteractiveHost(), []);
        
        //Assert
        var configMap = await client.CoreV1.ReadNamespacedConfigMapAsync(DestinationConfigMapName, commandNamespace);
        var secret = await client.CoreV1.ReadNamespacedSecretAsync(DestinationSecretName, commandNamespace);

        configMap.Data.TryGetValue("validationKey", out var validationKeyFromKubernetesConfigMap);
        validationKeyFromKubernetesConfigMap.Should().Be(validationKey);
        
        secret.Data.TryGetValue("validationKey", out var validationKeyFromKubernetesSecret);
        validationKeyFromKubernetesSecret.Should().Equal(Encoding.UTF8.GetBytes(validationKey));
    }

    async Task CreateCommandNamespace(string name)
    {
        await client.CoreV1.CreateNamespaceAsync(new V1Namespace
        {
            Metadata = new V1ObjectMeta
            {
                Name = name
            }
        });
    }
    
    async Task<V1ConfigMap> CreateConfigmap(string name, string ns, Dictionary<string, string>? data = null)
    {
        return await client.CoreV1.CreateNamespacedConfigMapAsync(new V1ConfigMap
        {
            Metadata = new V1ObjectMeta
            {
                Name = name
            },
            Data = data
        }, ns);
    }
    
    async Task<V1Secret> CreateSecret(string name, string ns, Dictionary<string, string>? data = null)
    {
        return await client.CoreV1.CreateNamespacedSecretAsync(new V1Secret
        {
            Metadata = new V1ObjectMeta
            {
                Name = name
            },
            StringData = data
        }, ns);
    }

    class KubernetesFileWrappedProvider(string filename) : IKubernetesClientConfigProvider
    {
        public KubernetesClientConfiguration Get()
        {
            return KubernetesClientConfiguration.BuildConfigFromConfigFile(filename);
        }
    }
}