using System;
using System.IO;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Services.Scripts.Security.Masking;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class BashScriptWorkspace : ScriptWorkspace
    {
        public BashScriptWorkspace(
            ScriptTicket scriptTicket,
            string workingDirectory,
            IOctopusFileSystem fileSystem,
            SensitiveValueMasker sensitiveValueMasker) :
            base(scriptTicket, workingDirectory, fileSystem, sensitiveValueMasker)
        {
        }

        const string BootstrapScriptFileName = "Bootstrap.sh";
        protected override string BootstrapScriptName => BootstrapScriptFileName;

        public override void BootstrapScript(string scriptBody)
        {
            scriptBody = scriptBody.Replace("\r\n", "\n");
            FileSystem.OverwriteFile(BootstrapScriptFilePath, scriptBody, Encoding.Default);
        }

        public static string GetBashBootstrapScriptFilePath(string workspaceDirectory)
            => Path.Combine(workspaceDirectory, BootstrapScriptFileName);
    }
}