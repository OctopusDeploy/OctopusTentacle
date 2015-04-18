using System;
using System.IO;
using System.Linq;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Tools;
using Octopus.Shared.Util;

namespace Octopus.Shared.Packages
{
    public class PackageDeltaFactory : IPackageDeltaFactory
    {
        readonly ILog log = Log.Octopus();
        readonly IOctopusFileSystem fileSystem;
        const string signatureCommandName = "signature";
        const string deltaCommandName = "delta";
        const string partialFileExtension = ".partial";
        readonly string currentWorkingDirectory;

        public PackageDeltaFactory(IOctopusFileSystem fileSystem, IHomeConfiguration config)
        {
            this.fileSystem = fileSystem;
            currentWorkingDirectory = Path.Combine(config.ApplicationSpecificHomeDirectory, "PackageCache");
            fileSystem.EnsureDirectoryExists(currentWorkingDirectory);
        }

        public string BuildSignature(string nearestPackageFilePath, ISemaphore semaphore)
        {
            var signatureFileName = Path.GetFileName(nearestPackageFilePath) + ".octosig";
            var fullSignatureFilePath = Path.Combine(currentWorkingDirectory, signatureFileName);

            using (semaphore.Acquire("Calamari.Signature." + signatureFileName, "Another process is currently building " + fullSignatureFilePath))
            {
                if (File.Exists(fullSignatureFilePath))
                {
                    log.VerboseFormat("Signature file {0} already exists, using {1}", signatureFileName, fullSignatureFilePath);
                    return fullSignatureFilePath;
                }

                log.VerboseFormat("Building signature file: {0} ", fullSignatureFilePath);
                log.VerboseFormat("  - Using nearest package: {0}", nearestPackageFilePath);

                var tempSignatureFilePath = fullSignatureFilePath + partialFileExtension;
                var octoDiff = new CliBuilder(OctoDiff.GetFullPath())
                    .Action(signatureCommandName)
                    .PositionalArgument(nearestPackageFilePath)
                    .PositionalArgument(tempSignatureFilePath)
                    .Build();

                var cmdResult = octoDiff.ExecuteCommand(currentWorkingDirectory);
                if (cmdResult.ExitCode != 0)
                {
                    fileSystem.DeleteFile(tempSignatureFilePath, DeletionOptions.TryThreeTimes);
                    throw new CommandLineException(cmdResult.ExitCode, cmdResult.Errors.ToList());
                }
                File.Move(tempSignatureFilePath, fullSignatureFilePath);
            }

            return fullSignatureFilePath;
        }

        public Stream BuildDelta(string newPackageFilePath, string signatureFilePath, string deltaFileName, ISemaphore semaphore)
        {
            var fullDeltaFilePath = Path.Combine(currentWorkingDirectory, deltaFileName);
            using (semaphore.Acquire("Calamari.Delta." + deltaFileName, "Another process is currently building delta file " + fullDeltaFilePath))
            {
                if (File.Exists(fullDeltaFilePath))
                {
                    log.VerboseFormat("Delta file {0} already exists, using file {1}", deltaFileName, fullDeltaFilePath);
                    return fileSystem.OpenFile(fullDeltaFilePath, FileAccess.Read);
                }

                log.VerboseFormat("Building delta file: {0}", fullDeltaFilePath);
                log.VerboseFormat("  - Using package: {0}.", newPackageFilePath);
                log.VerboseFormat("  - Using signature: {0}", signatureFilePath);

                var tempDeltaFilePath = fullDeltaFilePath + partialFileExtension;
                var octoDiff = new CliBuilder(OctoDiff.GetFullPath())
                    .Action(deltaCommandName)
                    .PositionalArgument(signatureFilePath)
                    .PositionalArgument(newPackageFilePath)
                    .PositionalArgument(tempDeltaFilePath)
                    .Build();

                var cmdResult = octoDiff.ExecuteCommand(currentWorkingDirectory);

                if (cmdResult.ExitCode != 0)
                {
                    fileSystem.DeleteFile(tempDeltaFilePath, DeletionOptions.TryThreeTimes);
                    throw new CommandLineException(cmdResult.ExitCode, cmdResult.Errors.ToList());
                }
                File.Move(tempDeltaFilePath, fullDeltaFilePath);
            }

            return fileSystem.OpenFile(fullDeltaFilePath, FileAccess.Read);
        }
    }
}
