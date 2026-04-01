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
            args.AppendFormat("-Command \"$ErrorActionPreference = 'Stop'; . {{. '{0}' {1}; if ((test-path variable:global:lastexitcode)) {{ exit $LastExitCode }}}}\"",
                escapedBootstrapFile,
                string.Join(" ", scriptArguments ?? new string[0]));
            
            return args.ToString();
        }
    }
}