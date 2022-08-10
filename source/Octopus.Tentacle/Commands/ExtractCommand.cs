using System;
using System.IO;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Packages;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Commands
{
    public class ExtractCommand : AbstractCommand
    {
        readonly Lazy<IPackageInstaller> packageInstaller;
        readonly ISystemLog log;
        string packageFile = null!;
        string destinationDirectory = null!;

        public ExtractCommand(Lazy<IPackageInstaller> packageInstaller, Lazy<IOctopusFileSystem> fileSystem, ISystemLog log, ILogFileOnlyLogger logFileOnlyLogger)
            : base(logFileOnlyLogger)
        {
            this.packageInstaller = packageInstaller;
            this.log = log;
            Options.Add("package=", "Package file", v =>
            {
                var fullPath = fileSystem.Value.GetFullPath(v);
                var directory = Path.GetDirectoryName(fullPath);
                if (directory == null)
                    throw new InvalidOperationException($"Unable to determine directory name from path {fullPath}");
                fileSystem.Value.EnsureDirectoryExists(directory);
                if (!fileSystem.Value.FileExists(fullPath))
                    throw new ControlledFailureException("Package not found: " + fullPath);

                log.Info("Package: " + fullPath);
                packageFile = fullPath;
            });
            Options.Add("destination=", "Destination directory", v =>
            {
                var fullPath = fileSystem.Value.GetFullPath(v);
                fileSystem.Value.EnsureDirectoryExists(fullPath);
                log.Info("Destination: " + fullPath);
                destinationDirectory = fullPath;
            });
        }

        const int ExtractRetries = 10;
        const int ExtractRetryDelay = 5000;

        protected override void Start()
        {
            if (string.IsNullOrWhiteSpace(packageFile))
                throw new ControlledFailureException("Please specify the package to extract via the --package argument.");
            if (string.IsNullOrWhiteSpace(destinationDirectory))
                throw new ControlledFailureException("Please specify the destination directory via the --destination argument.");

            for (int tryCount = 0; tryCount < ExtractRetries; tryCount++)
            {
                try
                {
                    var extracted = packageInstaller.Value.Install(packageFile, destinationDirectory, log, true);
                    log.Info($"{extracted:n0} files extracted");
                    return;
                }
                catch (Exception ex)
                {
                    log.Warn(ex, $"Failed to extract package to '{destinationDirectory}'");
                    if (tryCount == ExtractRetries - 1)
                    {
                        throw;
                    }
                    Thread.Sleep(ExtractRetryDelay);
                }
            }
        }
    }
}
