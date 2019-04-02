using System;

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
    }
}
