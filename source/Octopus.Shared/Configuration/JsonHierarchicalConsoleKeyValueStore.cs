using System;

namespace Octopus.Shared.Configuration
{
    public class JsonHierarchicalConsoleKeyValueStore : JsonHierarchicalKeyValueStore
    {
        private readonly Action<string> writer;

        public JsonHierarchicalConsoleKeyValueStore() 
            : this(Console.WriteLine)
        {
        }

        public JsonHierarchicalConsoleKeyValueStore(Action<string> writer)
            : base(false, true)
        {
            this.writer = writer;
        }

        protected override void WriteSerializedData(string serializedData)
        {
            writer(serializedData);
        }
    }
}
