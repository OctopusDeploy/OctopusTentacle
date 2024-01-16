using System;

namespace Octopus.Tentacle.Configuration.Instances
{
    public interface IAggregatableKeyValueStore
    {
        (bool foundResult, TData? value) TryGet<TData>(string name, ProtectionLevel protectionLevel = ProtectionLevel.None);
    }
}