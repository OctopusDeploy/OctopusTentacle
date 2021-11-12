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
        protected readonly IOctopusFileSystem FileSystem;

        TimeSpan scriptMutexAcquireTimeout = ScriptIsolationMutex.NoTimeout;

        public ScriptWorkspace(string workingDirectory, IOctopusFileSystem fileSystem)
        {
            WorkingDirectory = workingDirectory;
            FileSystem = fileSystem;
            fileSystem.EnsureDiskHasEnoughFreeSpace(workingDirectory);
            BootstrapScriptFilePath = Path.Combine(workingDirectory, BootstrapScriptName);
        }

        protected virtual string BootstrapScriptName => "Bootstrap.ps1";

        public NetworkCredential? RunAs { get; set; }

        public IDictionary<string, string> CustomEnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public ScriptIsolationLevel IsolationLevel { get; set; }

        public TimeSpan ScriptMutexAcquireTimeout
        {
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

        public string[]? ScriptArguments { get; set; }

        public string WorkingDirectory { get; }

        public string BootstrapScriptFilePath { get; }

        public virtual void BootstrapScript(string scriptBody)
        {
            // default is UTF8noBOM but powershell doesn't interpret that correctly
            FileSystem.OverwriteFile(BootstrapScriptFilePath, scriptBody, Encoding.UTF8);
        }

        public string ResolvePath(string fileName)
        {
            var path = Path.Combine(WorkingDirectory, fileName);
            var directory = Path.GetDirectoryName(path);
            if (directory != null)
                FileSystem.EnsureDirectoryExists(directory);
            return path;
        }

        public void Delete()
        {
            FileSystem.DeleteDirectory(WorkingDirectory, DeletionOptions.TryThreeTimesIgnoreFailure);
        }
    }
}
