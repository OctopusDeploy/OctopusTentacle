using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            ScriptTicket scriptTicket,
            string workingDirectory,
            IOctopusFileSystem fileSystem,
            SensitiveValueMasker sensitiveValueMasker)
        {
            ScriptTicket = scriptTicket;
            WorkingDirectory = workingDirectory;
            FileSystem = fileSystem;
            SensitiveValueMasker = sensitiveValueMasker;
            fileSystem.EnsureDiskHasEnoughFreeSpace(workingDirectory);
        }

        protected virtual string BootstrapScriptName => "Bootstrap.ps1";

        const string LogFileName = "Output.log";
        public string LogFilePath => GetLogFilePath(WorkingDirectory);
        public void WriteFile(string filename, string contents) => FileSystem.OverwriteFile(ResolvePath(filename), contents);

        public void CopyFile(string sourceFilePath, string destFileName) => FileSystem.CopyFile(sourceFilePath, ResolvePath(destFileName));

        public Stream OpenFileStreamForReading(string filename) => FileSystem.OpenFile(ResolvePath(filename), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        public bool FileExists(string filename)
        {
            return FileSystem.FileExists(ResolvePath(filename));
        }

        public static string GetLogFilePath(string workingDirectory) => Path.Combine(workingDirectory, LogFileName);

        public ScriptTicket ScriptTicket { get; }

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

        public async Task Delete(CancellationToken cancellationToken)
        {
            await FileSystem.DeleteDirectory(WorkingDirectory, cancellationToken, DeletionOptions.TryThreeTimesIgnoreFailure);

            // It appears that the FileSystem.DeleteDirectory method can fail to delete the directory in cases where Directory.Delete(recursive: true) can be successful.
            // Leaving the existing code as we would need to understand more about the intent of the logic before changing it but adding in another best effort attempt to delete the directory.
            if (Directory.Exists(WorkingDirectory))
            {
                try
                {
                    Directory.Delete(WorkingDirectory, true);
                }
                catch
                {
                    // Best effort cleanup so don't throw
                }
            }
        }

        public IScriptLog CreateLog()
        {
            return new ScriptLog(ResolvePath(LogFileName), FileSystem, SensitiveValueMasker);
        }
    }
}
