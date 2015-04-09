using System;
using System.IO;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Packages
{
    public interface IPackageExtractor
    {
        void Install(string packageFile, string directory, ILog log, bool suppressNestedScriptWarning, out int filesExtracted);
        void Install(Stream packageFile, string directory, ILog log, bool suppressNestedScriptWarning, out int filesExtracted);
    }
}