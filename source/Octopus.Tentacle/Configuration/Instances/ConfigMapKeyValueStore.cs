#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using k8s;
using k8s.Models;
using Newtonsoft.Json;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Configuration.Instances
{
    class ConfigMapKeyValueStore : IWritableKeyValueStore, IAggregatableKeyValueStore
    {
        readonly string @namespace;
        const string Name = "tentacle-config";
        readonly k8s.Kubernetes client;

        V1ConfigMap configMap;

        IDictionary<string, string> ConfigMapData => configMap.Data ?? (configMap.Data = new Dictionary<string, string>());

        public ConfigMapKeyValueStore(string @namespace, ISystemLog log)
        {
            this.@namespace = @namespace;
            var kubeConfig = KubernetesClientConfiguration.InClusterConfig();
            client = new k8s.Kubernetes(kubeConfig);
            V1ConfigMap? config;
            try
            {
                config = client.CoreV1.ReadNamespacedConfigMap(Name, @namespace);
            }
            catch
            {
                log.Verbose($"ConfigMap for Tentacle Configuration not found for namespace {@namespace}, creating new ConfigMap.");
                config = null;
            }

            configMap = config ?? client.CoreV1.CreateNamespacedConfigMap(new V1ConfigMap { Metadata = new V1ObjectMeta { Name = Name, NamespaceProperty = @namespace }}, @namespace);
        }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return ConfigMapData.TryGetValue(name, out var value) ? value : null;
        }

        public TData? Get<TData>(string name, TData? defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            var result = TryGet<TData>(name, protectionLevel);

            return result.foundResult ? result.value : defaultValue;
        }

        public void WriteTo(IWritableKeyValueStore outputStore)
        {
            foreach (var kvp in ConfigMapData)
            {
                outputStore.Set(kvp.Key, kvp.Value);
            }
        }

        public bool Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            configMap.Data[name] = value;
            return Save();
        }

        public bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return Set(name, JsonConvert.SerializeObject(value), protectionLevel);
        }

        public bool Remove(string name)
        {
            return ConfigMapData.Remove(name) && Save();
        }

        public bool Save()
        {
            configMap = client.CoreV1.ReplaceNamespacedConfigMap(configMap, Name, @namespace);
            return true;
        }

        public (bool foundResult, TData? value) TryGet<TData>(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            var value = Get(name, protectionLevel);

            if (value == null) return (false, default);

            try
            {
                return value is TData data ? (true, data) : (true, JsonConvert.DeserializeObject<TData>(value));
            }
            catch
            {
                return (false, default);
            }
        }
    }
}
#endif