#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using k8s.Models;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Configuration.Instances
{
    class ConfigMapKeyValueStore : IWritableKeyValueStore, IAggregatableKeyValueStore
    {
        public delegate ConfigMapKeyValueStore Factory(string @namespace);

        readonly IKubernetesV1ConfigMapService configMapService;
        const string Name = "tentacle-config";

        V1ConfigMap configMap;

        IDictionary<string, string> ConfigMapData => configMap.Data ?? (configMap.Data = new Dictionary<string, string>());

        public ConfigMapKeyValueStore(string @namespace, IKubernetesV1ConfigMapService configMapService, ISystemLog log)
        {
            this.configMapService = configMapService;
            V1ConfigMap? config;
            try
            {
                config = configMapService.Read(Name, @namespace).GetAwaiter().GetResult();
            }
            catch
            {
                log.Verbose($"ConfigMap for Tentacle Configuration not found for namespace {@namespace}, creating new ConfigMap.");
                config = null;
            }

            configMap = config ?? configMapService.Create(
                    new V1ConfigMap
                    {
                        Metadata = new V1ObjectMeta { Name = Name, NamespaceProperty = @namespace }
                    })
                .GetAwaiter().GetResult();
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
            configMap = configMapService.Replace(configMap).GetAwaiter().GetResult();
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