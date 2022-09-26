using System;
using System.Collections.Generic;
using Octopus.Configuration;

namespace Octopus.Tentacle.Configuration
{
    public class JsonHierarchicalConsoleKeyValueStore : JsonHierarchicalKeyValueStore
    {
        private readonly Action<string> writer;

        public JsonHierarchicalConsoleKeyValueStore()
            : this(Console.WriteLine)
        {
        }

        internal JsonHierarchicalConsoleKeyValueStore(Action<string> writer)
            : base(false, JsonSerialization.GetDefaultSerializerSettings(), true)
        {
            this.writer = writer;
        }

        protected override void WriteSerializedData(string serializedData)
        {
            writer(serializedData);
        }

        public override TData? Get<TData>(string name, TData? defaultValue, ProtectionLevel protectionLevel = ProtectionLevel.None) where TData : default
        {
            throw new NotSupportedException($"This store is a write-only store, because it is only intended for displaying formatted content to the console. Please use {nameof(JsonHierarchicalFileKeyValueStore)} if you need a readable store.");
        }

        protected override void LoadSettings(IDictionary<string, object?> settingsToFill)
        {
            throw new NotSupportedException($"This store is a write-only store, because it is only intended for displaying formatted content to the console. Please use {nameof(JsonHierarchicalFileKeyValueStore)} if you need a readable store.");
        }
    }
}