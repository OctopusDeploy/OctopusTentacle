using System;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Diagnostics.Masking;
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

        protected override string BootstrapScriptName => "Bootstrap.sh";

        public override void BootstrapScript(string scriptBody)
        {
            scriptBody = scriptBody.Replace("\r\n", "\n");
            FileSystem.OverwriteFile(BootstrapScriptFilePath, scriptBody, Encoding.Default);
        }
    }
}