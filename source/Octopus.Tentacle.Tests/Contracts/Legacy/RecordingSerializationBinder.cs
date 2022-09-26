using System;
using Newtonsoft.Json.Serialization;

namespace Octopus.Tentacle.Tests.Communications
{
    internal class RecordingSerializationBinder : DefaultSerializationBinder
    {
        public string? AssemblyName { get; private set; }
        public string TypeName { get; private set; }

        public override Type BindToType(string? assemblyName, string typeName)
        {
            AssemblyName = assemblyName;
            TypeName = typeName;
            return null;
        }
    }
}