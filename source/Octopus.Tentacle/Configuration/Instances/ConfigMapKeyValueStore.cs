using System;
using System.Collections.Generic;
using System.Threading;
using k8s.Models;
using Newtonsoft.Json;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Configuration.Crypto;

namespace Octopus.Tentacle.Configuration.Instances
{
    class ConfigMapKeyValueStore : IWritableKeyValueStore, IAggregatableKeyValueStore
    {

        readonly IKubernetesConfigMapService configMapService;
        readonly IKubernetesMachineKeyEncryptor encryptor;
        const string Name = "tentacle-config";

        readonly Lazy<V1ConfigMap> configMap;
        IDictionary<string, string> ConfigMapData => configMap.Value.Data ??= new Dictionary<string, string>();

        public ConfigMapKeyValueStore(IKubernetesConfigMapService configMapService, IKubernetesMachineKeyEncryptor encryptor)
        {
            this.configMapService = configMapService;
            this.encryptor = encryptor;
            configMap = new Lazy<V1ConfigMap>(() => configMapService.TryGet(Name, CancellationToken.None).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException($"Unable to retrieve Tentacle Configuration from config map for namespace {KubernetesConfig.Namespace}"));
        }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return ConfigMapData.TryGetValue(name, out var value) ? DecryptIfRequired(value, protectionLevel) : null;
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
            if (value is null)
            {
                return Remove(name);
            }

            ConfigMapData[name] = EncryptIfRequired(value, protectionLevel);
            return Save();
        }

        public bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return Set(name, JsonConvert.SerializeObject(value), protectionLevel);
        }

        public bool Remove(string name)
        {
            if (ConfigMapData.ContainsKey(name))
            {
                return ConfigMapData.Remove(name) && Save();
            }

            return false;
        }

        public bool Save()
        {
            configMapService.Patch(Name, ConfigMapData, CancellationToken.None).GetAwaiter().GetResult();
            return true;
        }

        string EncryptIfRequired(string input, ProtectionLevel protectionLevel)
        {
            return protectionLevel == ProtectionLevel.MachineKey ? encryptor.Encrypt(input) : input;
        }

        string DecryptIfRequired(string input, ProtectionLevel protectionLevel)
        {
            return protectionLevel == ProtectionLevel.MachineKey ? encryptor.Decrypt(input) : input;
        }
    }
}