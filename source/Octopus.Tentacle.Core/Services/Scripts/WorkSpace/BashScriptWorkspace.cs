using System;
using System.IO;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Services.Scripts;
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
            // Inject PowerShell startup detection code if the special comment is present
            // This works for pwsh (PowerShell Core) on Linux/Mac
            var processedScriptBody = scriptBody;
            if (PowerShellStartupDetection.ContainsSpecialComment(scriptBody))
            {
                processedScriptBody = PowerShellStartupDetection.InjectDetectionCode(scriptBody, WorkingDirectory);
                
                // Create the "should run" file to signal that the script should proceed
                var shouldRunFile = PowerShellStartupDetection.GetShouldRunFilePath(WorkingDirectory);
                FileSystem.OverwriteFile(shouldRunFile, "");
            }
            
            processedScriptBody = processedScriptBody.Replace("\r\n", "\n");
            FileSystem.OverwriteFile(BootstrapScriptFilePath, processedScriptBody, Encoding.Default);
        }

        public static string GetBashBootstrapScriptFilePath(string workspaceDirectory)
            => Path.Combine(workspaceDirectory, BootstrapScriptFileName);
    }
}