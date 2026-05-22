using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Kubernetes.Crypto;

namespace Octopus.Tentacle.Kubernetes.Configuration
{
    class ConfigMapKeyValueStore : IWritableKeyValueStore, IAggregatableKeyValueStore
    {
        readonly IKubernetesConfigMapService configMapService;
        readonly IKubernetesMachineKeyEncryptor encryptor;
        static string Name => KubernetesConfig.TentacleConfigMapName;

        IDictionary<string, string> ConfigMapData { get; }

        ConfigMapKeyValueStore(IKubernetesConfigMapService configMapService, IKubernetesMachineKeyEncryptor encryptor, IDictionary<string, string> configMapData)
        {
            this.configMapService = configMapService;
            this.encryptor = encryptor;
            ConfigMapData = configMapData;
        }

        public static async Task<ConfigMapKeyValueStore> CreateAsync(IKubernetesConfigMapService configMapService, IKubernetesMachineKeyEncryptor encryptor, CancellationToken cancellationToken)
        {
            await encryptor.InitializeAsync(cancellationToken);
            var configMap = await configMapService.TryGet(Name, cancellationToken);
            var configMapData = configMap?.Data ?? new Dictionary<string, string>();
            return new ConfigMapKeyValueStore(configMapService, encryptor, configMapData);
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
            => SetAsync(name, value, protectionLevel, CancellationToken.None).GetAwaiter().GetResult();

        public bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
            => SetAsync(name, value, protectionLevel, CancellationToken.None).GetAwaiter().GetResult();

        public bool Remove(string name)
            => RemoveAsync(name, CancellationToken.None).GetAwaiter().GetResult();

        public bool Save()
            => SaveAsync(CancellationToken.None).GetAwaiter().GetResult();

        public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
        {
            await configMapService.Patch(Name, ConfigMapData, cancellationToken);
            return true;
        }

        public async Task<bool> SetAsync(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None, CancellationToken cancellationToken = default)
        {
            if (value is null)
                return await RemoveAsync(name, cancellationToken);
            ConfigMapData[name] = EncryptIfRequired(value, protectionLevel);
            return await SaveAsync(cancellationToken);
        }

        public async Task<bool> SetAsync<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None, CancellationToken cancellationToken = default)
        {
            return await SetAsync(name, JsonConvert.SerializeObject(value), protectionLevel, cancellationToken);
        }

        public async Task<bool> RemoveAsync(string name, CancellationToken cancellationToken = default)
        {
            if (ConfigMapData.ContainsKey(name))
            {
                return ConfigMapData.Remove(name) && await SaveAsync(cancellationToken);
            }
            return false;
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
