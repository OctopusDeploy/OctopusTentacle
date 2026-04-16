using System;
using Octopus.Tentacle.Core.Services.Scripts.Shell;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class TestPwshShell : IShell
    {
        public string Name => "pwsh";

        public string GetFullPath()
        {
            return "pwsh";
        }

        public string FormatCommandArguments(string bootstrapFile, string[]? scriptArguments, bool allowInteractive)
        {
            var args = new System.Text.StringBuilder();
            
            if (!allowInteractive)
                args.Append("-NonInteractive ");
            
            args.Append("-NoProfile ");
            args.Append("-NoLogo ");
            args.Append("-ExecutionPolicy Unrestricted ");
            
            var escapedBootstrapFile = bootstrapFile.Replace("'", "''");
            
            // This is all copied from PowerShell.cs, the intention here is that this matches that as best we can for
            // testing powershell stuff on linux/mac for local dev.
            
            // $ErrorActionPreference = 'Stop': make all PS errors terminating.
            // `. { . 'file' args }`: double dot-source — outer `. { }` runs the block in current scope
            //   so $LastExitCode set inside the bootstrap script remains visible after it exits.
            // `if (test-path variable:global:lastexitcode)`: $LastExitCode only exists if a native
            //   process ran; guard prevents `exit $null` on pure-PS scripts.
            args.AppendFormat("-Command \"$ErrorActionPreference = 'Stop'; . {{. '{0}' {1}; if ((test-path variable:global:lastexitcode)) {{ exit $LastExitCode }}}}\"",
                escapedBootstrapFile,
                string.Join(" ", scriptArguments ?? new string[0]));
            
            return args.ToString();
        }
    }
}
