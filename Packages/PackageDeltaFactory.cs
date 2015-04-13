using System;
using System.IO;
using System.Linq;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Tools;
using Octopus.Shared.Util;

namespace Octopus.Shared.Packages
{
    public class PackageDeltaFactory : IPackageDeltaFactory
    {
        readonly ILog log = Log.Octopus();
        readonly IOctopusFileSystem fileSystem;
        readonly ISemaphore semaphore = new SystemSemaphore();
        readonly string signatureCommandName = "signature";
        readonly string deltaCommandName = "delta";
        readonly string currentWorkingDirectory;

        public PackageDeltaFactory(IOctopusFileSystem fileSystem, IPackageStore packageStore)
        {
            this.fileSystem = fileSystem;
            currentWorkingDirectory = packageStore.GetPackagesDirectory();
        }

        public string BuildSignature(string nearestPackageFilePath)
        {
            var signatureFilePath = Path.GetFileName(nearestPackageFilePath) + ".octosig";
            var fullPath = Path.Combine(currentWorkingDirectory, signatureFilePath);
            if (!File.Exists(fullPath))
            {
                log.VerboseFormat("Building signature file: {0} ", fullPath);
                log.VerboseFormat("  - Using nearest package: {0}", nearestPackageFilePath);
                using (semaphore.Acquire("Calamari:Signature: " + signatureFilePath, "Another process is currently building " + fullPath))
                {
                    var octoDiff = new CliBuilder(OctoDiff.GetFullPath())
                        .Action(signatureCommandName)
                        .PositionalArgument(nearestPackageFilePath)
                        .PositionalArgument(fullPath)
                        .Build();

                    var cmdResult = octoDiff.ExecuteCommand(currentWorkingDirectory);
                    if (cmdResult.ExitCode != 0)
                    {
                        fileSystem.DeleteFile(signatureFilePath, DeletionOptions.TryThreeTimes);
                        throw new CommandLineException(cmdResult.ExitCode, cmdResult.Errors.ToList());
                    }
                }
            }

            return fullPath;
        }

        public Stream BuildDelta(string newPackageFilePath, string signatureFilePath, string deltaFilePath)
        {
            var fullPath = Path.Combine(currentWorkingDirectory, deltaFilePath);
            if (!File.Exists(fullPath))
            {
                log.VerboseFormat("Building delta file: {0}", fullPath);
                log.VerboseFormat("  - Using package: {0}.", newPackageFilePath);
                log.VerboseFormat("  - Using signature: {0}", signatureFilePath);
                using (semaphore.Acquire("Calamari:Delta: " + deltaFilePath, "Another process is currently building delta file " + fullPath))
                {
                    var octoDiff = new CliBuilder(OctoDiff.GetFullPath())
                        .Action(deltaCommandName)
                        .PositionalArgument(signatureFilePath)
                        .PositionalArgument(newPackageFilePath)
                        .PositionalArgument(fullPath)
                        .Build();

                    var cmdResult = octoDiff.ExecuteCommand(currentWorkingDirectory);

                    if (cmdResult.ExitCode != 0)
                    {
                        fileSystem.DeleteFile(fullPath, DeletionOptions.TryThreeTimes);
                        throw new CommandLineException(cmdResult.ExitCode, cmdResult.Errors.ToList());
                    }
                }
            }

            return fileSystem.OpenFile(fullPath, FileAccess.Read);
        }
    }
}
