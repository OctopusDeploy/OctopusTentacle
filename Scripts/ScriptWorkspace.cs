using System;
using System.IO;
using System.Text;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class ScriptWorkspace : IScriptWorkspace
    {
        const string BootstrapScriptName = "Bootstrap.ps1";
        readonly IOctopusFileSystem fileSystem;

        public ScriptWorkspace(string workingDirectory, IOctopusFileSystem fileSystem)
        {
            WorkingDirectory = workingDirectory;
            this.fileSystem = fileSystem;
            fileSystem.EnsureDiskHasEnoughFreeSpace(workingDirectory);
            BootstrapScriptFilePath = Path.Combine(workingDirectory, BootstrapScriptName);
        }

        public ScriptIsolationLevel IsolationLevel { get; set; }

        TimeSpan scriptMutexAcquireTimeout;
        public TimeSpan ScriptMutexAcquireTimeout {
            get { return scriptMutexAcquireTimeout; }
            set
            {
                if (value == TimeSpan.Zero) // backwards compatability with old Server versions
                {
                    scriptMutexAcquireTimeout = ScriptIsolationMutex.NoTimeout;
                    return;
                }

                scriptMutexAcquireTimeout = value;
            }
        }

        public string[] ScriptArguments { get; set; }

        public string WorkingDirectory { get; }

        public string BootstrapScriptFilePath { get; }

        public void BootstrapScript(string scriptBody)
        {
            // default is UTF8noBOM but powershell doesn't interpret that correctly
            fileSystem.OverwriteFile(BootstrapScriptFilePath, scriptBody, Encoding.UTF8);
        }

        public string ResolvePath(string fileName)
        {
            var path = Path.Combine(WorkingDirectory, fileName);
            var directory = Path.GetDirectoryName(path);
            fileSystem.EnsureDirectoryExists(directory);
            return path;
        }

        public void Delete()
        {
            fileSystem.PurgeDirectory(WorkingDirectory, DeletionOptions.TryThreeTimesIgnoreFailure);
            fileSystem.DeleteDirectory(WorkingDirectory, DeletionOptions.TryThreeTimesIgnoreFailure);
        }
    }
}