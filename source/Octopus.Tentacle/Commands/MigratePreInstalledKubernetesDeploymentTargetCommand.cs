using System;
using System.Net;
using Octopus.Tentacle.Startup;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public class MigratePreInstalledKubernetesDeploymentTargetCommand : AbstractCommand
    {
        readonly ISystemLog log;
        readonly Lazy<IKubernetesClientConfigProvider> configProvider;

        string? sourceConfigMapName;
        string? sourceSecretName;
        string? destinationConfigMapName;        
        string? destinationSecretName;
        string? @namespace;

        public MigratePreInstalledKubernetesDeploymentTargetCommand(Lazy<IKubernetesClientConfigProvider> configProvider, ISystemLog log, ILogFileOnlyLogger logFileOnlyLogger) : base(logFileOnlyLogger)
        {
            this.log = log;
            this.configProvider = configProvider;
            
            Options.Add("source-config-map-name=", "The name of the source config map (created by the pre-installation of the agent)", v => sourceConfigMapName = v);
            Options.Add("source-secret-name=", "The name of the source secret (created by the pre-installation of the agent)", v => sourceSecretName = v);
            Options.Add("destination-config-map-name=", "The name of the destination config map", v => destinationConfigMapName = v);
            Options.Add("destination-secret-name=", "The name of the destination secret", v => destinationSecretName = v);
            Options.Add("namespace=", "The namespace to use for the migration", v => @namespace = v);
        }
        
        // This command is only used as a way to programatically copy the config map and secret from the pre-installation hook to the new agent
        // It does not use the IWritableTentacleConfiguration so we don't accidentally generate any new keys
        protected override void Start()
        {
            if (!PlatformDetection.Kubernetes.IsRunningAsKubernetesAgent)
            {
                throw new ControlledFailureException("This command can only be run from within a Kubernetes agent.");
            }

            // Check that the sources and destinations are different
            if (sourceSecretName == destinationSecretName || sourceConfigMapName == destinationConfigMapName)
            {
                throw new ControlledFailureException("Source and destination names must be different.");
            }
            
            var migrationNamespace = @namespace ?? KubernetesConfig.Namespace;
            
            var config = configProvider.Value.Get();
            var client = new k8s.Kubernetes(config);
            var sourceConfigMap = TryGetCoreV1Object(() => client.CoreV1.ReadNamespacedConfigMap(sourceConfigMapName, migrationNamespace));
            var sourceSecret = TryGetCoreV1Object(() => client.CoreV1.ReadNamespacedSecret(sourceSecretName, migrationNamespace));
            var destinationConfigMap = TryGetCoreV1Object(() => client.CoreV1.ReadNamespacedConfigMap(destinationConfigMapName, migrationNamespace));
            var destinationSecret = TryGetCoreV1Object(() => client.CoreV1.ReadNamespacedSecret(destinationSecretName, migrationNamespace));

            var (migrateData, reason) = ShouldMigrateData(sourceConfigMap, sourceSecret, destinationConfigMap, destinationSecret);
            if (!migrateData)
            {
                log.Info(reason);
                return;
            }

            // Copy the data from the source to the destination
            destinationConfigMap!.Data = sourceConfigMap!.Data;
            destinationSecret!.Data = sourceSecret!.Data;
            client.CoreV1.ReplaceNamespacedConfigMap(destinationConfigMap, destinationConfigMapName, migrationNamespace);
            client.CoreV1.ReplaceNamespacedSecret(destinationSecret, destinationSecretName, migrationNamespace);
            
            // Delete the sources (they are no longer needed)
            client.CoreV1.DeleteNamespacedConfigMap(sourceConfigMapName, migrationNamespace);
            client.CoreV1.DeleteNamespacedSecret(sourceSecretName, migrationNamespace);
            
            log.Info("Migration complete.");
        }
        
        public static (bool ShouldMigrate, string Reason) ShouldMigrateData(V1ConfigMap? sourceConfigMap, V1Secret? sourceSecret, V1ConfigMap? destinationConfigMap, V1Secret? destinationSecret)
        {
            // Check that the sources exist
            if (sourceConfigMap is null || sourceSecret is null)
            {
                return (false, "Source config map or secret not found, skipping migration");
            }
            
            // Check if the destinations exist
            if (destinationConfigMap is null || destinationSecret is null)
            {
                return (false, "destination config map or secret not found, skipping migration.");
            }


            // Check if the destination is already registered
            if (destinationConfigMap.Data is not null && destinationConfigMap.Data.TryGetValue("Tentacle.Services.IsRegistered", out var isRegistered) && string.Equals(isRegistered, "true", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Tentacle is already registered, skipping registration.");
            }

            return (true, string.Empty);
        }

        static T? TryGetCoreV1Object<T>(Func<T> kubernetesFunc) where T : class
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