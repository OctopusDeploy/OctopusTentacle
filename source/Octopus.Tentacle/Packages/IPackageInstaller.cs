using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Packages
{
    public interface IPackageInstaller
    {
        Task<int> Install(string packageFile, string directory, ILog log, bool suppressNestedScriptWarning, CancellationToken cancellationToken);
        Task<int> Install(Stream packageStream, string directory, ILog log, bool suppressNestedScriptWarning, CancellationToken cancellationToken);
    }
}