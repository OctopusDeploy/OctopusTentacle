using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Security;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptWorkspaceFactory : IScriptWorkspaceFactory
    {
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
            if (!PlatformDetection.IsRunningOnWindows)
                return new BashScriptWorkspace(FindWorkingDirectory(ticket), fileSystem, sensitiveValueMasker);

            return new ScriptWorkspace(FindWorkingDirectory(ticket), fileSystem, sensitiveValueMasker);
        }

        public IScriptWorkspace PrepareWorkspace(
            ScriptTicket ticket,
            string scriptBody,
            Dictionary<ScriptType, string> scripts,
            ScriptIsolationLevel isolationLevel,
            TimeSpan scriptMutexAcquireTimeout,
            string? scriptMutexName,
            string[]? scriptArguments,
            List<ScriptFile> files)
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


            files.ForEach(file => SaveFileToDisk(workspace, file));

            return workspace;
        }

        void SaveFileToDisk(IScriptWorkspace workspace, ScriptFile scriptFile)
        {
#pragma warning disable CS0612
            if (scriptFile.EncryptionPassword == null)
            {
                scriptFile.Contents.Receiver().SaveTo(workspace.ResolvePath(scriptFile.Name));
            }
            else
            {
                scriptFile.Contents.Receiver().Read(stream =>
                {
                    using var reader = new StreamReader(stream);
                    fileSystem.WriteAllBytes(workspace.ResolvePath(scriptFile.Name), new AesEncryption(scriptFile.EncryptionPassword).Encrypt(reader.ReadToEnd()));
                });
            }
#pragma warning restore CS0612
        }

        string FindWorkingDirectory(ScriptTicket ticket)
        {
            var work = GetWorkingDirectoryPath(ticket);
            fileSystem.EnsureDirectoryExists(work);
            return work;
        }

        public string GetWorkingDirectoryPath(ScriptTicket ticket)
        {
            return fileSystem.GetFullPath(Path.Combine(home.HomeDirectory ?? "", "Work", ticket.TaskId));
        }
    }
}