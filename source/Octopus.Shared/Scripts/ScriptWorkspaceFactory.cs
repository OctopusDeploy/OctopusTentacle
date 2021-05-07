using System;
using System.IO;
using Octopus.Shared.Configuration;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class ScriptWorkspaceFactory : IScriptWorkspaceFactory
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IHomeConfiguration home;

        public ScriptWorkspaceFactory(IOctopusFileSystem fileSystem, IHomeConfiguration home)
        {
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
            var work = fileSystem.GetFullPath(Path.Combine(home.HomeDirectory, "Work", ticket.TaskId));
            fileSystem.EnsureDirectoryExists(work);
            return work;
        }
    }
}