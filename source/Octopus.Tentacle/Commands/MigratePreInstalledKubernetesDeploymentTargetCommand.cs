using System;
using System.Net;
using Octopus.Diagnostics;
using Octopus.Tentacle.Startup;
using k8s;
using k8s.Autorest;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Commands
{
    public class MigratePreInstalledKubernetesDeploymentTargetCommand : AbstractCommand
    {
        readonly ISystemLog log;
        readonly IKubernetesClientConfigProvider configProvider;

        string? sourceConfigMapName;
        string? sourceSecretName;
        string? destinationConfigMapName;        
        string? destinationSecretName;

        public MigratePreInstalledKubernetesDeploymentTargetCommand(IKubernetesClientConfigProvider configProvider, ISystemLog log, ILogFileOnlyLogger logFileOnlyLogger) : base(logFileOnlyLogger)
        {
            this.log = log;
            this.configProvider = configProvider;
            
            Options.Add("source-config-map-name=", "The name of the source config map (created by the pre-installation of the agent)", v => sourceConfigMapName = v);
            Options.Add("source-secret-name=", "The name of the source secret (created by the pre-installation of the agent)", v => sourceSecretName = v);
            Options.Add("destination-config-map-name=", "The name of the destination config map", v => destinationConfigMapName = v);
            Options.Add("destination-secret-name=", "The name of the destination secret", v => destinationSecretName = v);
        }
        
        // This command is only used as a way to programatically copy the config map and secret from the pre-installation hook to the new agent
        // It does not access tentacle configuration so that it doesn't 
        protected override void Start()
        {
            // Check that the sources and destinations are different
            if (sourceSecretName == destinationSecretName || sourceConfigMapName == destinationConfigMapName)
            {
                log.Error("Source and destination names must be different.");
                return;
            }
            
            var config = configProvider.Get();
            var client = new k8s.Kubernetes(config);
            
            // Check that the sources exist
            var sourceConfigMap = TryGetCoreV1Object(() => client.CoreV1.ReadNamespacedConfigMap(sourceConfigMapName, KubernetesConfig.Namespace));
            var sourceSecret = TryGetCoreV1Object(() => client.CoreV1.ReadNamespacedSecret(sourceSecretName, KubernetesConfig.Namespace));
            if (sourceConfigMap is null || sourceSecret is null)
            {
                log.Info("Source config map or secret not found, skipping migration.");
                return;
            }
            
            // Check if the destinations exist
            var destinationConfigMap = TryGetCoreV1Object(() => client.CoreV1.ReadNamespacedConfigMap(destinationConfigMapName, KubernetesConfig.Namespace));
            var destinationSecret = TryGetCoreV1Object(() => client.CoreV1.ReadNamespacedSecret(destinationSecretName, KubernetesConfig.Namespace));
            if (destinationConfigMap is null || destinationSecret is null)
            {
                log.Info("destination config map or secret not found, skipping migration.");
                return;
            }


            // Check if the destination is already registered
            if (destinationConfigMap.Data is not null && destinationConfigMap.Data.TryGetValue("Tentacle.Services.IsRegistered", out var isRegistered) && isRegistered == "True")
            {
                log.Info("Tentacle is already registered, skipping registration.");
                return;
            }
            
            // Copy the data from the source to the destination
            destinationConfigMap.Data = sourceConfigMap.Data;
            destinationSecret.Data = sourceSecret.Data;
            client.CoreV1.ReplaceNamespacedConfigMap(destinationConfigMap, destinationConfigMapName, KubernetesConfig.Namespace);
            client.CoreV1.ReplaceNamespacedSecret(destinationSecret, destinationSecretName, KubernetesConfig.Namespace);
            
            // Delete the sources (they are no longer needed)
            client.CoreV1.DeleteNamespacedConfigMap(sourceConfigMapName, KubernetesConfig.Namespace);
            client.CoreV1.DeleteNamespacedSecret(sourceSecretName, KubernetesConfig.Namespace);
            
            log.Info("Migration complete.");
        }

        T? TryGetCoreV1Object<T>(Func<T> kubernetesFunc) where T : class
        {
            try
            {
                return kubernetesFunc();
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
    
    
}