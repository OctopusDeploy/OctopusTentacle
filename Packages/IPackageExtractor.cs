using System;
using Octopus.Platform.Diagnostics;

namespace Octopus.Shared.Packages
{
    public interface IPackageExtractor
    {
        void Install(string packageFile, string directory, ILog log, bool suppressNestedScriptWarning, out int filesExtracted);
    }
}