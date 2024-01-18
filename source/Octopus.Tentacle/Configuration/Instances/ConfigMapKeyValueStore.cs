using System;
using System.Collections.Generic;
using System.Threading;
using k8s.Models;
using Newtonsoft.Json;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Configuration.Instances
{
    class ConfigMapKeyValueStore : IWritableKeyValueStore, IAggregatableKeyValueStore
    {

        readonly IKubernetesV1ConfigMapService configMapService;
        const string Name = "tentacle-config";

        V1ConfigMap configMap;
        IDictionary<string, string> ConfigMapData => configMap.Data ??= new Dictionary<string, string>();

        public ConfigMapKeyValueStore(IKubernetesV1ConfigMapService configMapService)
        {
            this.configMapService = configMapService;
            configMap = configMapService.TryGet(Name, CancellationToken.None).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {KubernetesConfig.Namespace}");
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
            configMap = configMapService.Patch(Name, ConfigMapData, CancellationToken.None).GetAwaiter().GetResult();
            return true;
        }
    }
}