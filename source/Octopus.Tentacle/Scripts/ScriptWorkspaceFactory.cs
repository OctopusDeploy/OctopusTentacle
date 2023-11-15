using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Security;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptWorkspaceFactory : IScriptWorkspaceFactory
    {
        public const string WorkDirectory = "Work";

        readonly IOctopusFileSystem fileSystem;
        readonly IHomeConfiguration home;
        readonly SensitiveValueMasker sensitiveValueMasker;

        public ScriptWorkspaceFactory(
            IOctopusFileSystem fileSystem,
            IHomeConfiguration home,
            SensitiveValueMasker sensitiveValueMasker)
        {
            if (home.ApplicationSpecificHomeDirectory == null)
                throw new ArgumentException($"{GetType().Name} cannot function without the HomeDirectory configured.", nameof(home));

            this.fileSystem = fileSystem;
            this.home = home;
            this.sensitiveValueMasker = sensitiveValueMasker;
        }

        public IScriptWorkspace GetWorkspace(ScriptTicket ticket)
        {
            var workingDirectory = FindWorkingDirectory(ticket);

            return CreateWorkspace(ticket, workingDirectory);
        }

        public async Task<IScriptWorkspace> PrepareWorkspace(
            ScriptTicket ticket,
            string scriptBody,
            Dictionary<ScriptType, string> scripts,
            ScriptIsolationLevel isolationLevel,
            TimeSpan scriptMutexAcquireTimeout,
            string? scriptMutexName,
            string[]? scriptArguments,
            List<ScriptFile> files,
            CancellationToken cancellationToken)
        {
            var workspace = GetWorkspace(ticket);
            workspace.IsolationLevel = isolationLevel;
            workspace.ScriptMutexAcquireTimeout = scriptMutexAcquireTimeout;
            workspace.ScriptArguments = scriptArguments;
            workspace.ScriptMutexName = scriptMutexName;

            if (PlatformDetection.IsRunningOnNix || PlatformDetection.IsRunningOnMac)
            {
                //TODO: This could be better
                workspace.BootstrapScript(scripts.ContainsKey(ScriptType.Bash)
                    ? scripts[ScriptType.Bash]
                    : scriptBody);
            }
            else
            {
                workspace.BootstrapScript(scriptBody);
            }


            foreach (var file in files)
            {
                await SaveFileToDisk(workspace, file, cancellationToken);
            }

            return workspace;
        }

        async Task SaveFileToDisk(IScriptWorkspace workspace, ScriptFile scriptFile, CancellationToken cancellationToken)
        {
            if (scriptFile.EncryptionPassword == null)
            {
                await scriptFile.Contents.Receiver().SaveToAsync(workspace.ResolvePath(scriptFile.Name), cancellationToken);
            }
            else
            {
                await scriptFile.Contents.Receiver().ReadAsync(async (stream, _) =>
                {
                    await Task.CompletedTask;

                    using var reader = new StreamReader(stream);
                    fileSystem.WriteAllBytes(workspace.ResolvePath(scriptFile.Name), new AesEncryption(scriptFile.EncryptionPassword).Encrypt(reader.ReadToEnd()));
                }, cancellationToken);
            }
        }

        string FindWorkingDirectory(ScriptTicket ticket)
        {
            var work = GetWorkingDirectoryPath(ticket);
            fileSystem.EnsureDirectoryExists(work);
            return work;
        }

        IScriptWorkspace CreateWorkspace(ScriptTicket scriptTicket, string workingDirectory)
        {
            if (!PlatformDetection.IsRunningOnWindows)
            {
                return new BashScriptWorkspace(scriptTicket, workingDirectory, fileSystem, sensitiveValueMasker);
            }

            return new ScriptWorkspace(scriptTicket, workingDirectory, fileSystem, sensitiveValueMasker);
        }

        public string GetWorkingDirectoryPath(ScriptTicket ticket)
        {
            var baseWorkingDirectory = GetBaseWorkingDirectory();
            return fileSystem.GetFullPath(Path.Combine(baseWorkingDirectory, ticket.TaskId));
        }
        
        string GetBaseWorkingDirectory()
        {
            return Path.Combine(home.HomeDirectory ?? "", WorkDirectory);
        }

        public List<IScriptWorkspace> GetUncompletedWorkspaces()
        {
            var baseWorkingDirectory = GetBaseWorkingDirectory();

            if (!Directory.Exists(baseWorkingDirectory))
            {
                return new List<IScriptWorkspace>();
            }

            var workspaceDirectories = Directory.GetDirectories(baseWorkingDirectory)
                .Where(IsUncompletedWorkspaceDirectory)
                .Select(CreateWorkspaceFromWorkspaceDirectory)
                .ToList();

            return workspaceDirectories;
        }

        static bool IsUncompletedWorkspaceDirectory(string workspaceDirectory)
        {
            var outputLogFilePath = ScriptWorkspace.GetLogFilePath(workspaceDirectory);
            return File.Exists(outputLogFilePath);
        }

        IScriptWorkspace CreateWorkspaceFromWorkspaceDirectory(string workspaceDirectory)
        {
            var workspaceDirectoryInfo = new DirectoryInfo(workspaceDirectory);
            var scriptTicket = new ScriptTicket(workspaceDirectoryInfo.Name);

            return CreateWorkspace(scriptTicket, workspaceDirectory);
        }
    }
}