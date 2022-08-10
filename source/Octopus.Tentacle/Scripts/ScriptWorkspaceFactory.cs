using System;
using System.IO;
using Octopus.Shared.Contracts;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptWorkspaceFactory : IScriptWorkspaceFactory
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IHomeConfiguration home;

        public ScriptWorkspaceFactory(IOctopusFileSystem fileSystem, IHomeConfiguration home)
        {
            if (home.ApplicationSpecificHomeDirectory == null)
                throw new ArgumentException($"{GetType().Name} cannot function without the HomeDirectory configured.", nameof(home));

            this.fileSystem = fileSystem;
            this.home = home;
        }

        public IScriptWorkspace GetWorkspace(ScriptTicket ticket)
        {
            if (!PlatformDetection.IsRunningOnWindows)
                return new BashScriptWorkspace(FindWorkingDirectory(ticket), fileSystem);

            return new ScriptWorkspace(FindWorkingDirectory(ticket), fileSystem);
        }

        string FindWorkingDirectory(ScriptTicket ticket)
        {
            var work = fileSystem.GetFullPath(Path.Combine(home.HomeDirectory ?? "", "Work", ticket.TaskId));
            fileSystem.EnsureDirectoryExists(work);
            return work;
        }
    }
}