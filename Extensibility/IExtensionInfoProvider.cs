using System;
using System.Collections.Generic;

namespace Octopus.Shared.Extensibility
{
    public interface IExtensionInfoProvider
    {
        IEnumerable<ExtensionInfo> LoadedExtensionData { get; }
    }
}