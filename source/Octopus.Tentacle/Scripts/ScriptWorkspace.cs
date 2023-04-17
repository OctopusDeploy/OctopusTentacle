using System;
using System.IO;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Services.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptWorkspace : IScriptWorkspace
    {
        protected readonly IOctopusFileSystem FileSystem;
        protected readonly SensitiveValueMasker SensitiveValueMasker;

        TimeSpan scriptMutexAcquireTimeout = ScriptIsolationMutex.NoTimeout;

        public ScriptWorkspace(
            string workingDirectory,
            IOctopusFileSystem fileSystem,
            SensitiveValueMasker sensitiveValueMasker)
        {
            WorkingDirectory = workingDirectory;
            FileSystem = fileSystem;
            SensitiveValueMasker = sensitiveValueMasker;
            fileSystem.EnsureDiskHasEnoughFreeSpace(workingDirectory);
        }

        protected virtual string BootstrapScriptName => "Bootstrap.ps1";

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

        public string? ScriptMutexName { get; set; }

        public string[]? ScriptArguments { get; set; }

        public string WorkingDirectory { get; }

        public string BootstrapScriptFilePath => Path.Combine(WorkingDirectory, BootstrapScriptName);

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

        public IScriptLog CreateLog()
        {
            return new ScriptLog(ResolvePath("Output.log"), FileSystem, SensitiveValueMasker);
        }
    }
}
