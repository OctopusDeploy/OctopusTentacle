using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Tests.Util
{
    public static class SerializationExtensionMethods
    {
        public static string ToJson<T>(this JsonSerializer jsonSerializer, T input)
        {
            var objectType = input != null ? input.GetType() : typeof(T);

            var sb = new StringBuilder();
            using var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(sw) { Formatting = jsonSerializer.Formatting };

            jsonSerializer.Serialize(jsonWriter, input, objectType);
            return sw.ToString();
        }

        public static T FromJson<T>(this JsonSerializer jsonSerializer, string json)
        {
            if (json is null) throw new ArgumentNullException(nameof(json));

            var objectType = typeof(T);

            return (T)jsonSerializer.FromJson(json, objectType);
        }

        public static object? FromJson(this JsonSerializer jsonSerializer, string json, Type objectType)
        {
            using var reader = new JsonTextReader(new StringReader(json));
            var output = jsonSerializer.Deserialize(reader, objectType);
            return output;
        }

        public static void Configure(this JsonSerializer jsonSerializer, JsonSerializerSettings settings)
        {
            // private static void ApplySerializerSettings(JsonSerializer serializer, JsonSerializerSettings settings)
            var applySerializerSettingsMethod = typeof(JsonSerializer).GetMethod("ApplySerializerSettings", BindingFlags.NonPublic | BindingFlags.Static)!;
            var args = new object[] { jsonSerializer, settings };
            applySerializerSettingsMethod.Invoke(null, args);
        }
    }
}