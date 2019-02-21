using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class ScriptWorkspace : IScriptWorkspace
    {
        protected virtual string BootstrapScriptName => "Bootstrap.ps1";
        
        protected readonly IOctopusFileSystem fileSystem;

        public ScriptWorkspace(string workingDirectory, IOctopusFileSystem fileSystem)
        {
            WorkingDirectory = workingDirectory;
            this.fileSystem = fileSystem;
            fileSystem.EnsureDiskHasEnoughFreeSpace(workingDirectory);
            BootstrapScriptFilePath = Path.Combine(workingDirectory, BootstrapScriptName);
        }

        public NetworkCredential RunAs { get; set; }

        public IDictionary<string, string> CustomEnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public ScriptIsolationLevel IsolationLevel { get; set; }

        TimeSpan scriptMutexAcquireTimeout = ScriptIsolationMutex.NoTimeout;
        public TimeSpan ScriptMutexAcquireTimeout {
            get => scriptMutexAcquireTimeout;
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

        public virtual void BootstrapScript(string scriptBody)
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