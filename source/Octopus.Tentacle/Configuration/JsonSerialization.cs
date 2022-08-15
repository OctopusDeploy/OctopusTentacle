using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Octopus.Tentacle.Configuration
{
    public static class JsonSerialization
    {
        public static JsonSerializerSettings GetDefaultSerializerSettings()
            => new JsonSerializerSettings
            {
                Converters = new JsonConverterCollection
                {
                    new StringEnumConverter()
                }
            };
    }
}