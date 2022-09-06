using Halibut.Transport.Protocol;

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
                var namespaceMappingBinder = new NamespaceMappingSerializationBinderDecorator(settings.SerializationBinder, LegacyNamespace, TentacleContracts.Namespace);
                var assemblyMappingBinder = new AssemblyMappingSerializationBinderDecorator(namespaceMappingBinder, LegacyAssembly, TentacleContracts.AssemblyName);
                settings.SerializationBinder = assemblyMappingBinder;
            });
        }
    }
}