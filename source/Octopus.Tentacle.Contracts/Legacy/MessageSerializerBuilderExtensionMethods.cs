using Halibut.Transport.Protocol;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.Legacy
{
    public static class MessageSerializerBuilderExtensionMethods
    {
        const string LegacyNamespace = "Octopus.Shared.Contracts";
        const string LegacyAssembly = "Octopus.Shared";
        public static MessageSerializerBuilder WithLegacyContractSupport(this MessageSerializerBuilder builder)
        {
            return builder.WithSerializerSettings(settings =>
            {
                AddLegacyContractSupportToJsonSerializer(settings);
            });
        }

        public static void AddLegacyContractSupportToJsonSerializer(JsonSerializerSettings settings)
        {
            var namespaceMappingBinder = new NamespaceMappingSerializationBinderDecorator(settings.SerializationBinder, LegacyNamespace, TentacleContracts.Namespace);
            var assemblyMappingBinder = new AssemblyMappingSerializationBinderDecorator(namespaceMappingBinder, LegacyAssembly, TentacleContracts.AssemblyName, LegacyNamespace, TentacleContracts.Namespace);
            settings.SerializationBinder = assemblyMappingBinder;
        }
    }
}