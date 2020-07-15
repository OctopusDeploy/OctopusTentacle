using System.Diagnostics;
using System.Text;
using Octopus.Shared.Util;

namespace Octopus.Shared.Scripts
{
    public class BashScriptWorkspace : ScriptWorkspace
    {
        protected override string BootstrapScriptName => "Bootstrap.sh";
            
        public BashScriptWorkspace(string workingDirectory, IOctopusFileSystem fileSystem) : base(workingDirectory, fileSystem)
        {
        }
        
        public override void BootstrapScript(string scriptBody)
        {
            scriptBody = scriptBody.Replace("\r\n", "\n");
            FileSystem.OverwriteFile(BootstrapScriptFilePath, scriptBody, Encoding.Default);
        }
    }
}