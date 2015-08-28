using System;
using System.IO;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class ScriptWorkspace : IScriptWorkspace
    {
        const string BootstrapScriptName = "Bootstrap.ps1";
        readonly string workingDirectory;
        readonly IOctopusFileSystem fileSystem;
        readonly string bootstrapScriptFilePath;

        public ScriptWorkspace(string workingDirectory, IOctopusFileSystem fileSystem)
        {
            this.workingDirectory = workingDirectory;
            this.fileSystem = fileSystem;
            fileSystem.EnsureDiskHasEnoughFreeSpace(workingDirectory);
            bootstrapScriptFilePath = Path.Combine(workingDirectory, BootstrapScriptName);
        }

        public ScriptIsolationLevel IsolationLevel { get; set; }

        public string[] ScriptArguments { get; set; }

        public string WorkingDirectory
        {
            get { return workingDirectory; }
        }

        public string BootstrapScriptFilePath
        {
            get { return bootstrapScriptFilePath; }
        }

        public void BootstrapScript(string scriptBody)
        {
            fileSystem.OverwriteFile(BootstrapScriptFilePath, scriptBody);
        }

        public string ResolvePath(string fileName)
        {
            var path = Path.Combine(workingDirectory, fileName);
            var directory = Path.GetDirectoryName(path);
            fileSystem.EnsureDirectoryExists(directory);
            return path;
        }

        public void Delete()
        {
            fileSystem.PurgeDirectory(workingDirectory, DeletionOptions.TryThreeTimesIgnoreFailure);
            fileSystem.DeleteDirectory(workingDirectory, DeletionOptions.TryThreeTimesIgnoreFailure);
        }
    }
}