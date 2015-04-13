using System;
using System.Collections.Generic;
using System.IO;
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
        readonly string octoDiffPath = "";
        readonly string signatureCommandName = "signature";
        readonly string deltaCommandName = "delta";
        readonly string currentWorkingDirectory;

        public PackageDeltaFactory(IOctopusFileSystem fileSystem, IPackageStore packageStore)
        {
            this.fileSystem = fileSystem;
            octoDiffPath = OctoDiff.GetFullPath();
            currentWorkingDirectory = packageStore.GetPackagesDirectory();
        }

        public string BuildSignature(string nearestPackageFilePath)
        {
            var signatureFilePath = Path.GetFileName(nearestPackageFilePath) + ".octosig";
            var fullPath = Path.Combine(currentWorkingDirectory, signatureFilePath);
            if (!File.Exists(fullPath))
            {
                log.VerboseFormat("Building signature file: {0} ", fullPath);
                using (semaphore.Acquire("Calamari:Signature: " + signatureFilePath, "Another process is currently building " + signatureFilePath))
                {
                    var arguments = OctoDiff.FormatCommandArguments(signatureCommandName, nearestPackageFilePath, signatureFilePath);
                    log.VerboseFormat("Running {0} {1}", octoDiffPath, arguments);
                    
                    var exitCode = SilentProcessRunner.ExecuteCommand(
                        octoDiffPath,
                        arguments,
                        currentWorkingDirectory,
                        output => log.Info(output),
                        error => log.Error(error));

                    if(exitCode != 0)
                        fileSystem.DeleteFile(signatureFilePath, DeletionOptions.TryThreeTimes);
                }
            }
            return signatureFilePath;
        }

        public Stream BuildDelta(string newPackageFilePath, string signatureFilePath, string deltaFilePath)
        {
            var fullPath = Path.Combine(currentWorkingDirectory, deltaFilePath);
            if (!File.Exists(fullPath))
            {
                log.VerboseFormat("Building delta file: {0}", fullPath);
                using (semaphore.Acquire("Calamari:Delta: " + deltaFilePath, "Another process is currently building delta file " + deltaFilePath))
                {
                    var arguments = OctoDiff.FormatCommandArguments(deltaCommandName, signatureFilePath, newPackageFilePath, deltaFilePath);
                        log.VerboseFormat("Running {0} {1}", octoDiffPath, arguments);
                    
                    var exitCode = SilentProcessRunner.ExecuteCommand(
                        octoDiffPath,
                        arguments,
                        currentWorkingDirectory,
                        output => log.Info(output),
                        error => log.Error(error));

                    if (exitCode != 0)
                        fileSystem.DeleteFile(deltaFilePath, DeletionOptions.TryThreeTimes);
                }
            }

            return fileSystem.OpenFile(deltaFilePath, FileAccess.Read);
        }
    }
}
