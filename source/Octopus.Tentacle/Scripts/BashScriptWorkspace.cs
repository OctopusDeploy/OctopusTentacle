using System;
using System.Text;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class BashScriptWorkspace : ScriptWorkspace
    {
        public BashScriptWorkspace(string workingDirectory, IOctopusFileSystem fileSystem) : base(workingDirectory, fileSystem)
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