#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using k8s;
using k8s.Models;
using Newtonsoft.Json;
using Octopus.Configuration;

namespace Octopus.Tentacle.Configuration.Instances
{
    class ConfigMapKeyValueStore : IWritableKeyValueStore, IAggregatableKeyValueStore
    {
        readonly string @namespace;
        readonly string name;
        readonly k8s.Kubernetes client;

        V1ConfigMap configMap;

        IDictionary<string, string> ConfigMapData => configMap.Data ?? (configMap.Data = new Dictionary<string, string>());

        public ConfigMapKeyValueStore(string instanceName)
        {
            @namespace = Environment.GetEnvironmentVariable("OCTOPUS__K8STENTACLE__NAMESPACE") ?? "octopus";
            var kubeConfig = KubernetesClientConfiguration.InClusterConfig();
            client = new k8s.Kubernetes(kubeConfig);
            name = $"{instanceName.ToLowerInvariant()}-configmap";
            V1ConfigMap? config;
            try
            {
                config = client.CoreV1.ReadNamespacedConfigMap(name, @namespace);
            }
            catch (Exception e)
            {
                Console.WriteLine($"ConfigMapKeyValueStore.ctor Exception: {e}");
                config = null;
            }

            configMap = config ?? client.CoreV1.CreateNamespacedConfigMap(new V1ConfigMap { Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = @namespace }}, @namespace);
            Console.WriteLine($"ConfigMapKeyValueStore.ctor ConfigMap loaded: {JsonConvert.SerializeObject(configMap)}");
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
            configMap = client.CoreV1.ReplaceNamespacedConfigMap(configMap, name, @namespace);
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
            catch (Exception e)
            {
                Console.WriteLine($"ConfigMapKeyValueStore.TryGet<TData>: Exception! {e}");
                return (false, default);
            }
        }
    }
}
#endif