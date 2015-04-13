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
            if (!File.Exists(Path.Combine(currentWorkingDirectory, signatureFilePath)))
            {
                log.VerboseFormat("Building signature file: {0} ", Path.Combine(currentWorkingDirectory, signatureFilePath));
                using (semaphore.Acquire("Building signature file: " + signatureFilePath, "Another process is currently building " + signatureFilePath))
                {
                    var exitCode = 0;
                    var errors = new List<string>();
                    try
                    {
                        var arguments = OctoDiff.FormatCommandArguments(signatureCommandName, nearestPackageFilePath, signatureFilePath);
                        log.VerboseFormat("Running {0} {1}", octoDiffPath, arguments);
                        exitCode = SilentProcessRunner.ExecuteCommand(
                            octoDiffPath,
                            arguments,
                            currentWorkingDirectory,
                            output => log.Info(output),
                            errors.Add);
                    }
                    catch (Exception ex)
                    {
                        errors.Insert(0, "An exception was thrown when invoking " + octoDiffPath + " " + signatureCommandName + ": " + ex.Message);
                        throw new CommandLineException(exitCode, errors);
                    }
                }
            }
            return signatureFilePath;
        }

        public Stream BuildDelta(string newPackageFilePath, string signatureFilePath, string deltaFilePath)
        {
            if (!File.Exists(Path.Combine(currentWorkingDirectory, deltaFilePath)))
            {
                log.VerboseFormat("Building delta file: {0}", Path.Combine(currentWorkingDirectory, deltaFilePath));
                using (semaphore.Acquire("Building delta file: " + deltaFilePath, "Another process is currently building delta file " + deltaFilePath))
                {
                    var exitCode = 0;
                    var errors = new List<string>();
                    try
                    {
                        var arguments = OctoDiff.FormatCommandArguments(deltaCommandName, signatureFilePath, newPackageFilePath, deltaFilePath);
                        log.VerboseFormat("Running {0} {1}", octoDiffPath, arguments);
                        exitCode = SilentProcessRunner.ExecuteCommand(
                            octoDiffPath,
                            arguments,
                            currentWorkingDirectory,
                            output => log.Info(output),
                            errors.Add);
                    }
                    catch (Exception ex)
                    {
                        errors.Insert(0, "An exception was thrown when invoking " + octoDiffPath + " " + deltaCommandName + ": " + ex.Message);
                        throw new CommandLineException(exitCode, errors);
                    }
                }
            }

            return fileSystem.OpenFile(deltaFilePath, FileAccess.Read);
        }
    }
}
