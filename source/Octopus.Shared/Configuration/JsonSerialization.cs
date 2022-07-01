using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Octopus.Shared.Configuration
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