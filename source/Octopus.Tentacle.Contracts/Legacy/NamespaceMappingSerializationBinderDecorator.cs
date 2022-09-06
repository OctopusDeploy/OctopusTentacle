using System;
using Newtonsoft.Json.Serialization;

namespace Octopus.Tentacle.Contracts.Legacy
{
    public class NamespaceMappingSerializationBinderDecorator : ISerializationBinder
    {
        readonly ISerializationBinder inner;
        readonly string fromNamespace;
        readonly string toNamespace;

        public NamespaceMappingSerializationBinderDecorator(ISerializationBinder? inner, string fromNamespace, string toNamespace)
        {
            this.inner = inner ?? new DefaultSerializationBinder();
            this.fromNamespace = fromNamespace;
            this.toNamespace = toNamespace;
        }

        public Type BindToType(string? assemblyName, string typeName)
        {
            var mappedNamespace = typeName.Replace(fromNamespace, toNamespace);

            var type = inner.BindToType(assemblyName, mappedNamespace);
            return type;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            inner.BindToName(serializedType, out assemblyName, out typeName);
            typeName = typeName?.Replace(toNamespace, fromNamespace);
        }
    }
}