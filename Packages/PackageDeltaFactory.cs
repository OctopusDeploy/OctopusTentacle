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
        const string SignatureCommandName = "signature";
        const string DeltaCommandName = "delta";
        const string PartialFileExtension = ".partial";
        readonly ILog log = Log.Octopus();
        readonly IOctopusFileSystem fileSystem;
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

                var tempSignatureFilePath = fullSignatureFilePath + PartialFileExtension;
                var octoDiff = new CliBuilder(OctoDiff.GetFullPath())
                    .Action(SignatureCommandName)
                    .PositionalArgument(nearestPackageFilePath)
                    .PositionalArgument(tempSignatureFilePath)
                    .Flag("progress")
                    .Build();

                var cmdResult = octoDiff.ExecuteCommand(currentWorkingDirectory);
                if (cmdResult.ExitCode != 0)
                {
                    fileSystem.DeleteFile(tempSignatureFilePath, DeletionOptions.TryThreeTimes);
                    log.Warn("The previous command returned a non-zero exit code of: " + cmdResult.ExitCode);
                    log.Warn("The command that failed was: " + octoDiff);
                    return null;
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

                var tempDeltaFilePath = fullDeltaFilePath + PartialFileExtension;
                var octoDiff = new CliBuilder(OctoDiff.GetFullPath())
                    .Action(DeltaCommandName)
                    .PositionalArgument(signatureFilePath)
                    .PositionalArgument(newPackageFilePath)
                    .PositionalArgument(tempDeltaFilePath)
                    .Flag("progress")
                    .Build();

                var cmdResult = octoDiff.ExecuteCommand(currentWorkingDirectory);
                if (cmdResult.ExitCode != 0)
                {
                    fileSystem.DeleteFile(tempDeltaFilePath, DeletionOptions.TryThreeTimes);
                    log.Warn("The previous command returned a non-zero exit code of: " + cmdResult.ExitCode);
                    log.Warn("The command that failed was: " + octoDiff);
                    return null;
                }
                File.Move(tempDeltaFilePath, fullDeltaFilePath);
            }

            return fileSystem.OpenFile(fullDeltaFilePath, FileAccess.Read);
        }
    }
}