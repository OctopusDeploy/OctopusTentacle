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

        /// <summary>
        /// The types we need to remap are cached here to avoid needing to re-create the type on every call, as we were seeing
        /// threads held up in the ctor of this type when under load.  
        /// </summary>
        static readonly ReMappedLegacyTypes ReMappedLegacyTypes = new(LegacyNamespace, TentacleContracts.Namespace); 

        public static void AddLegacyContractSupportToJsonSerializer(JsonSerializerSettings settings)
        {
            var namespaceMappingBinder = new NamespaceMappingSerializationBinderDecorator(settings.SerializationBinder, LegacyNamespace, TentacleContracts.Namespace, ReMappedLegacyTypes);
            var assemblyMappingBinder = new AssemblyMappingSerializationBinderDecorator(namespaceMappingBinder, LegacyAssembly, TentacleContracts.AssemblyName, ReMappedLegacyTypes);
            settings.SerializationBinder = assemblyMappingBinder;
        }
    }
}