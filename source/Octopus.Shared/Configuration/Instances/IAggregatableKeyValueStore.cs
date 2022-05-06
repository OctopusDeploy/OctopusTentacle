using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration.Instances
{
    public interface IAggregatableKeyValueStore
    {
        (bool foundResult, TData? value) TryGet<TData>(string name, ProtectionLevel protectionLevel = ProtectionLevel.None);
    }
}