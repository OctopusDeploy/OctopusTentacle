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
        const string signatureCommandName = "signature";
        const string deltaCommandName = "delta";
        const string partialFileExtension = ".partial";
        readonly string currentWorkingDirectory;

        public PackageDeltaFactory(IOctopusFileSystem fileSystem, IPackageStore packageStore)
        {
            this.fileSystem = fileSystem;
            currentWorkingDirectory = packageStore.GetPackagesDirectory();
        }

        public string BuildSignature(string nearestPackageFilePath)
        {
            var signatureFileName = Path.GetFileName(nearestPackageFilePath) + ".octosig";
            var fullSignatureFilePath = Path.Combine(currentWorkingDirectory, signatureFileName);
            if (!File.Exists(fullSignatureFilePath))
            {
                log.VerboseFormat("Building signature file: {0} ", fullSignatureFilePath);
                log.VerboseFormat("  - Using nearest package: {0}", nearestPackageFilePath);
                var tempSignatureFilePath = fullSignatureFilePath + partialFileExtension;
                using (semaphore.Acquire("Calamari:Signature: " + signatureFileName, "Another process is currently building " + fullSignatureFilePath))
                {
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
            }

            return fullSignatureFilePath;
        }

        public Stream BuildDelta(string newPackageFilePath, string signatureFilePath, string deltaFileName)
        {
            var fullDeltaFilePath = Path.Combine(currentWorkingDirectory, deltaFileName);
            if (!File.Exists(fullDeltaFilePath))
            {
                var tempDeltaFilePath = fullDeltaFilePath + partialFileExtension;
                log.VerboseFormat("Building delta file: {0}", fullDeltaFilePath);
                log.VerboseFormat("  - Using package: {0}.", newPackageFilePath);
                log.VerboseFormat("  - Using signature: {0}", signatureFilePath);
                using (semaphore.Acquire("Calamari:Delta: " + deltaFileName, "Another process is currently building delta file " + fullDeltaFilePath))
                {
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
            }

            return fileSystem.OpenFile(fullDeltaFilePath, FileAccess.Read);
        }
    }
}
