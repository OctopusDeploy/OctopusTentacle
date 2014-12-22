using System;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Packages
{
    public interface IPackageExtractor
    {
        void Install(string packageFile, string directory, ILog log, bool suppressNestedScriptWarning, out int filesExtracted);
    }
}