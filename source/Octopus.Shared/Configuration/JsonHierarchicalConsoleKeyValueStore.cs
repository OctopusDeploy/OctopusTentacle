using System;

namespace Octopus.Shared.Configuration
{
    public class JsonHierarchicalConsoleKeyValueStore : JsonHierarchicalKeyValueStore
    {
        public JsonHierarchicalConsoleKeyValueStore() : base((bool) false, (bool) true)
        {
        }

        protected override void WriteSerializedData(string serializedData)
        {
            Console.WriteLine(serializedData);
        }
    }
}
