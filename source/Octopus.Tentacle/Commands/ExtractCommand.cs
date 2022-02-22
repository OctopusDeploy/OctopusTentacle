using System;
using System.IO;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Packages;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Commands
{
    public class ExtractCommand : AbstractCommand
    {
        private const int ExtractRetries = 10;
        private const int ExtractRetryDelay = 5000;
        private readonly Lazy<IPackageInstaller> packageInstaller;
        private readonly ISystemLog log;
        private string packageFile;
        private string destinationDirectory;

        public ExtractCommand(Lazy<IPackageInstaller> packageInstaller, Lazy<IOctopusFileSystem> fileSystem, ISystemLog log, ILogFileOnlyLogger logFileOnlyLogger)
            : base(logFileOnlyLogger)
        {
            this.packageInstaller = packageInstaller;
            this.log = log;
            Options.Add("package=", "Package file", v =>
            {
                var fullPath = fileSystem.Value.GetFullPath(v);
                fileSystem.Value.EnsureDirectoryExists(Path.GetDirectoryName(fullPath));
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

        protected override void Start()
        {
            if (string.IsNullOrWhiteSpace(packageFile))
                throw new ControlledFailureException("Please specify the package to extract via the --package argument.");
            if (string.IsNullOrWhiteSpace(destinationDirectory))
                throw new ControlledFailureException("Please specify the destination directory via the --destination argument.");

            for (var tryCount = 0; tryCount < ExtractRetries; tryCount++)
                try
                {
                    var extracted = packageInstaller.Value.Install(packageFile, destinationDirectory, log, true);
                    log.Info($"{extracted:n0} files extracted");
                    return;
                }
                catch (Exception ex)
                {
                    log.Warn(ex, $"Failed to extract package to '{destinationDirectory}'");
                    if (tryCount == ExtractRetries - 1) throw;
                    Thread.Sleep(ExtractRetryDelay);
                }
        }
    }
}