using System;
using System.IO;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Packages
{
    public interface IPackageInstaller
    {
        int Install(string packageFile, string directory, ILog log, bool suppressNestedScriptWarning);
        int Install(Stream packageStream, string directory, ILog log, bool suppressNestedScriptWarning);
    }
}