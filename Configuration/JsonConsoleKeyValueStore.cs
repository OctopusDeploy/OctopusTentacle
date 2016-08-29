using System;

namespace Octopus.Shared.Configuration
{
    public class JsonConsoleKeyValueStore : JsonKeyValueStore
    {
        public JsonConsoleKeyValueStore(bool useHierarchicalOutput) : base(useHierarchicalOutput)
        {
        }

        protected override void WriteSerializedData(string serializedData)
        {
            Console.WriteLine(serializedData);
        }
    }
}