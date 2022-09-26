using System;
using Newtonsoft.Json.Serialization;

namespace Octopus.Tentacle.Contracts.Legacy
{
    public class AssemblyMappingSerializationBinderDecorator : ISerializationBinder
    {
        private readonly ISerializationBinder inner;
        private readonly string fromAssembly;
        private readonly string toAssembly;

        public AssemblyMappingSerializationBinderDecorator(ISerializationBinder inner, string fromAssembly, string toAssembly)
        {
            this.inner = inner;
            this.fromAssembly = fromAssembly;
            this.toAssembly = toAssembly;
        }

        public Type BindToType(string? assemblyName, string typeName)
        {
            var mappedNamespace = assemblyName?.Replace(fromAssembly, toAssembly);
            var type = inner.BindToType(mappedNamespace, typeName);
            return type;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            inner.BindToName(serializedType, out assemblyName, out typeName);
            assemblyName = assemblyName?.Replace(toAssembly, fromAssembly);
        }
    }
}