using System;
using Newtonsoft.Json.Serialization;

namespace Octopus.Tentacle.Contracts.Legacy
{
    public class AssemblyMappingSerializationBinderDecorator : ISerializationBinder
    {
        readonly ISerializationBinder inner;
        readonly string fromAssembly;
        readonly string toAssembly;
        readonly string fromNamespace;
        readonly string toNamespace;
        private readonly ReMappedLegacyTypes reMappedLegacyTypes;

        public AssemblyMappingSerializationBinderDecorator(ISerializationBinder inner, string fromAssembly, string toAssembly, string fromNamespace, string toNamespace)
        {
            this.inner = inner;
            this.fromAssembly = fromAssembly;
            this.toAssembly = toAssembly;
            this.fromNamespace = fromNamespace;
            this.toNamespace = toNamespace;
            this.reMappedLegacyTypes = new ReMappedLegacyTypes(fromNamespace, toNamespace);
        }
        
        public Type BindToType(string? assemblyName, string typeName)
        {
            var mappedNamespace = reMappedLegacyTypes.ShouldRemap(typeName)
                ? assemblyName?.Replace(fromAssembly, toAssembly)
                : assemblyName;
            var type = inner.BindToType(mappedNamespace, typeName);
            return type;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            inner.BindToName(serializedType, out assemblyName, out typeName);
            assemblyName = reMappedLegacyTypes.ShouldRemap(typeName)
                ? assemblyName?.Replace(toAssembly, fromAssembly)
                : assemblyName;
        }
    }
}