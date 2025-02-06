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
    
    [Test]
    public async Task MigrationFromPreinstallHookShouldCopyData()
    {
        //Arrange
        var kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        var commandNamespace = Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, commandNamespace);
        var commandToRun = new MigratePreInstalledKubernetesDeploymentTargetCommand(new Lazy<IKubernetesClientConfigProvider>(kubernetesConfigClient), systemLog, new LogFileOnlyLogger());
        var client = new k8s.Kubernetes(kubernetesConfigClient.Get());
        var validationKey = Guid.NewGuid().ToString("N");
        var sourceConfigMapData = new Dictionary<string, string>
        {
            {"validationKey", validationKey},
            {"Tentacle.Services.IsRegistered", "true"}
        };
        var sourceSecretData = new Dictionary<string, string>
        {
            {"machine-iv", "testData"},
            {"machine-key", validationKey}
        };
        const string sourceConfigMapName = "tentacle-config-pre";
        const string sourceSecretName = "tentacle-secret-pre";
        const string destinationConfigMapName = "tentacle-config";
        const string destinationSecretName = "tentacle-secret";

        
        string[] commandArguments = [
            $"--source-config-map-name={sourceConfigMapName}", 
            $"--source-secret-name={sourceSecretName}",
            $"--destination-config-map-name={destinationConfigMapName}",
            $"--destination-secret-name={destinationSecretName}",
            $"--namespace={commandNamespace}"
        ];

        //Act
        await CreateCommandNamespace(client, commandNamespace);
        
        //Sources
        await CreateConfigmap(client, sourceConfigMapName, commandNamespace, sourceConfigMapData);
        await CreateSecret(client, sourceSecretName, commandNamespace, sourceSecretData);

        
        // Targets
        await CreateConfigmap(client, destinationConfigMapName, commandNamespace);
        await CreateSecret(client, destinationSecretName, commandNamespace);
        
        commandToRun.Start(commandArguments, new NoninteractiveHost(), []);
        
        //Assert
        var configMap = await client.CoreV1.ReadNamespacedConfigMapAsync(destinationConfigMapName, commandNamespace);
        var secret = await client.CoreV1.ReadNamespacedSecretAsync(destinationSecretName, commandNamespace);

        configMap.Data.TryGetValue("validationKey", out var validationKeyFromKubernetes);
        validationKeyFromKubernetes.Should().NotBeNull().And.Be(validationKey);
        
        secret.Data.TryGetValue("machine-key", out var hostKeyFromKubernetes);
        hostKeyFromKubernetes.Should().NotBeNull().And.Equal(Encoding.UTF8.GetBytes(validationKey));
    }
    
    [Test]
    public async Task MigrationFromPreinstallHookShouldOnlyRunWhenNotRegistered()
    {
        //Arrange
        var kubernetesConfigClient = new KubernetesFileWrappedProvider(KubernetesTestsGlobalContext.Instance.KubeConfigPath);
        var commandNamespace = Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(KubernetesConfig.NamespaceVariableName, commandNamespace);
        var commandToRun = new MigratePreInstalledKubernetesDeploymentTargetCommand(new Lazy<IKubernetesClientConfigProvider>(kubernetesConfigClient), systemLog, new LogFileOnlyLogger());
        var client = new k8s.Kubernetes(kubernetesConfigClient.Get());
        var validationKey = Guid.NewGuid().ToString("N");
        var sourceConfigMapData = new Dictionary<string, string>
        {
            {"validationKey", "should-not-be-here"},
            {"Tentacle.Services.IsRegistered", "true"}
        };
        var sourceSecretData = new Dictionary<string, string>
        {
            {"machine-iv", "testData"},
            {"machine-key", validationKey}
        };
        var destinationConfigMapData = new Dictionary<string, string>
        {
            {"validationKey", validationKey},
            {"Tentacle.Services.IsRegistered", "true"}
        };
        var destinationSecretData = new Dictionary<string, string>
        {
            {"machine-iv", "testData"},
            {"machine-key", validationKey}
        };


        const string sourceConfigMapName = "tentacle-config-pre";
        const string sourceSecretName = "tentacle-secret-pre";
        const string destinationConfigMapName = "tentacle-config";
        const string destinationSecretName = "tentacle-secret";

        
        string[] commandArguments = [
            $"--source-config-map-name={sourceConfigMapName}", 
            $"--source-secret-name={sourceSecretName}",
            $"--destination-config-map-name={destinationConfigMapName}",
            $"--destination-secret-name={destinationSecretName}",
            $"--namespace={commandNamespace}"
        ];

        //Act
        await CreateCommandNamespace(client, commandNamespace);
        
        //Sources
        await CreateConfigmap(client, sourceConfigMapName, commandNamespace, sourceConfigMapData);
        await CreateSecret(client, sourceSecretName, commandNamespace, sourceSecretData);

        
        // Targets
        await CreateConfigmap(client, destinationConfigMapName, commandNamespace, destinationConfigMapData);
        await CreateSecret(client, destinationSecretName, commandNamespace, destinationSecretData);
        
        commandToRun.Start(commandArguments, new NoninteractiveHost(), []);
        
        //Assert
        var configMap = await client.CoreV1.ReadNamespacedConfigMapAsync(destinationConfigMapName, commandNamespace);
        var secret = await client.CoreV1.ReadNamespacedSecretAsync(destinationSecretName, commandNamespace);

        configMap.Data.TryGetValue("validationKey", out var validationKeyFromKubernetes);
        validationKeyFromKubernetes.Should().NotBeNull().And.Be(validationKey);
        
        secret.Data.TryGetValue("machine-key", out var hostKeyFromKubernetes);
        hostKeyFromKubernetes.Should().BeNullOrEmpty();
    }

    async Task CreateCommandNamespace(k8s.Kubernetes client, string name)
    {
        await client.CoreV1.CreateNamespaceAsync(new V1Namespace
        {
            Metadata = new V1ObjectMeta
            {
                Name = name
            }
        });
    }
    
    async Task<V1ConfigMap> CreateConfigmap(k8s.Kubernetes client, string name, string ns, Dictionary<string, string>? data = null)
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
    
    async Task<V1Secret> CreateSecret(k8s.Kubernetes client, string name, string ns, Dictionary<string, string>? data = null)
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