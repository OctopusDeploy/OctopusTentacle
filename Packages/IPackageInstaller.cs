using System;
using System.IO;
using Octopus.Server.Extensibility.HostServices.Diagnostics;

namespace Octopus.Shared.Packages
{
    public interface IPackageInstaller
    {
        int Install(string packageFile, string directory, ILog log, bool suppressNestedScriptWarning);
        int Install(Stream packageStream, string directory, ILog log, bool suppressNestedScriptWarning);
    }
}