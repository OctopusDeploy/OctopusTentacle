using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public class JsonConsoleKeyValueStore : JsonFlatKeyValueStore
    {
        private Action<string> writer;

        public JsonConsoleKeyValueStore() 
            : this(Console.WriteLine)
        {
        }

        public JsonConsoleKeyValueStore(Action<string> writer)
            : base(false, true)
        {
            this.writer = writer;
        }

        protected override void WriteSerializedData(string serializedData)
        {
            writer(serializedData);
        }
        
        public override TData Get<TData>(string name, TData defaultValue, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            throw new NotSupportedException($"This store is a write-only store, because it is only intended for displaying formatted content to the console. Please use {nameof(JsonFileKeyValueStore)} if you need a readable store.");
        }
    }
}
