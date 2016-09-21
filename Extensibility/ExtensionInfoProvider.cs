using System;
using System.Collections.Generic;

namespace Octopus.Shared.Extensibility
{
    public class ExtensionInfoProvider : IExtensionInfoProvider
    {
        readonly List<ExtensionInfo> info;

        public ExtensionInfoProvider()
        {
            info = new List<ExtensionInfo>();
        }

        public void AddExtensionData(ExtensionInfo data)
        {
            info.Add(data);
        }

        public IEnumerable<ExtensionInfo> LoadedExtensionData => info;
    }
}