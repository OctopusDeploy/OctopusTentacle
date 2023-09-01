using System;
using System.IO;
using System.Text;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class BashScriptWorkspace : ScriptWorkspace
    {
        public BashScriptWorkspace(
            string workingDirectory,
            IOctopusFileSystem fileSystem,
            SensitiveValueMasker sensitiveValueMasker) :
            base(workingDirectory, fileSystem, sensitiveValueMasker)
        {
        }

        protected override string BootstrapScriptName => "Bootstrap.sh";

        public override void BootstrapScript(string scriptBody)
        {
            scriptBody = scriptBody.Replace("\r\n", "\n");
            FileSystem.OverwriteFile(BootstrapScriptFilePath, scriptBody, Encoding.Default);
        }

        public override string WriteFile(string filename, string contents)
        {
            var path = Path.Combine(WorkingDirectory, filename);

            contents = contents.Replace("\r\n", "\n");
            FileSystem.OverwriteFile(path, contents, Encoding.Default);

            return path;
        }
    }
}