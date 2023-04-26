using System;
using Newtonsoft.Json.Serialization;

namespace Octopus.Tentacle.Contracts.Legacy
{
    public class NamespaceMappingSerializationBinderDecorator : ISerializationBinder
    {
        readonly ISerializationBinder inner;
        readonly string fromNamespace;
        readonly string toNamespace;
        private readonly ReMappedLegacyTypes reMappedLegacyTypes;

        public NamespaceMappingSerializationBinderDecorator(ISerializationBinder? inner, string fromNamespace, string toNamespace)
        {
            this.inner = inner ?? new DefaultSerializationBinder();
            this.fromNamespace = fromNamespace;
            this.toNamespace = toNamespace;
            this.reMappedLegacyTypes = new ReMappedLegacyTypes(fromNamespace, toNamespace);
        }

        public Type BindToType(string? assemblyName, string typeName)
        {
            var mappedNamespace = reMappedLegacyTypes.ShouldRemap(typeName)
                ? typeName.Replace(fromNamespace, toNamespace)
                : typeName;

            var type = inner.BindToType(assemblyName, mappedNamespace);
            return type;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            inner.BindToName(serializedType, out assemblyName, out typeName);
            typeName = reMappedLegacyTypes.ShouldRemap(typeName)
                ? typeName?.Replace(toNamespace, fromNamespace)
                : typeName;
        }
    }
}