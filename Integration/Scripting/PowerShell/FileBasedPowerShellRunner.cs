using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Octopus.Shared.Configuration;
using Octopus.Shared.ServiceMessages;
using Octopus.Shared.Util;

namespace Octopus.Shared.Integration.Scripting.PowerShell
{
    public class FileBasedPowerShellRunner : IScriptRunner
    {
        readonly IProxyConfiguration proxyConfiguration;

        public FileBasedPowerShellRunner(IProxyConfiguration proxyConfiguration)
        {
            this.proxyConfiguration = proxyConfiguration;
        }

        public string[] GetSupportedExtensions()
        {
            return new[] { "ps1" };
        }

        public ScriptExecutionResult Execute(ScriptArguments arguments)
        {
            var bootstrapFile = PrepareBootstrapFile(arguments);
            
            try
            {
                var commandArguments = new StringBuilder();
                commandArguments.Append("-NonInteractive ");
                commandArguments.Append("-NoLogo ");
                commandArguments.Append("-ExecutionPolicy Unrestricted ");
                commandArguments.AppendFormat("-File \"{0}\"", bootstrapFile);

                var filter = new ScriptExecutionOutputFilter(arguments.OutputStream);

                var errorWritten = false;
                var exit = SilentProcessRunner.ExecuteCommand("powershell.exe", commandArguments.ToString(), arguments.WorkingDirectory,
                    filter.WriteLine,
                    error => { 
                        arguments.OutputStream.OnWritten("ERROR: " + error);
                        errorWritten = true;
                    });

                return new ScriptExecutionResult(exit, errorWritten, filter.OutputVariables, filter.CreatedArtifacts);
            }
            finally
            {
                File.Delete(bootstrapFile);
            }
        }

        // We create a temporary file to invoke the PowerShell script with the variables loaded
        string PrepareBootstrapFile(ScriptArguments arguments)
        {
            var bootstrapFile = Path.Combine(arguments.WorkingDirectory, "Bootstrap." + Guid.NewGuid() + ".ps1");
            
            using (var writer = new StreamWriter(bootstrapFile))
            {
                writer.WriteLine("## Variables:");
                
                WriteLocalVariables(arguments, writer);

                WriteVariableDictionary(arguments, writer);

                writer.WriteLine();
                writer.WriteLine("## Functions:");

                writer.WriteLine(EmbeddedFunctions);
                writer.WriteLine();
                writer.WriteLine(GetProxyConfigurationScript());
                writer.WriteLine();

                writer.WriteLine("## Invoke:");
                
                writer.WriteLine(". \"" + arguments.ScriptFilePath + "\"");
                writer.WriteLine("if ((test-path variable:global:lastexitcode)) { exit $LastExitCode }");
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return bootstrapFile;
        }

        static void WriteVariableDictionary(ScriptArguments arguments, StreamWriter writer)
        {
            writer.WriteLine("$OctopusParameters = New-Object 'System.Collections.Generic.Dictionary[String,String]' (,[System.StringComparer]::OrdinalIgnoreCase)");
            foreach (var variable in arguments.Variables)
            {
                writer.WriteLine("$OctopusParameters[" + EncodeValue(variable.Key) + "] = " + EncodeValue(variable.Value));
            }
        }

        static void WriteLocalVariables(ScriptArguments arguments, StreamWriter writer)
        {
            foreach (var variable in arguments.Variables)
            {
                // This is the way we used to fix up the identifiers - people might still rely on this behavior
                var legacyKey = new string(variable.Key.Where(char.IsLetterOrDigit).ToArray());

                // This is the way we should have done it
                var smartKey = new string(variable.Key.Where(IsValidPowerShellIdentifierChar).ToArray());

                if (legacyKey != smartKey)
                {
                    writer.WriteLine("$" + legacyKey + " = " + EncodeValue(variable.Value));
                }

                writer.WriteLine("$" + smartKey + " = " + EncodeValue(variable.Value));
            }
        }

        static string EncodeValue(string value)
        {
            if (value == null)
                return "$null";

            return "[System.Text.Encoding]::UTF8.GetString( [Convert]::FromBase64String( \"" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)) + "\" ) )";
        }

        static bool IsValidPowerShellIdentifierChar(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }

        const string EmbeddedFunctions =
@"function Encode-ServiceMessageValue([string]$value)
{
	$valueBytes = [System.Text.Encoding]::UTF8.GetBytes($value)
	return [Convert]::ToBase64String($valueBytes)
}

function Set-OctopusVariable([string]$name, [string]$value) 
{ 	
    $name = Encode-ServiceMessageValue($name)
    $value = Encode-ServiceMessageValue($value)

	Write-Output ""##octopus[setVariable name='$name' value='$value']""
}

function New-OctopusArtifact([string]$path) 
{ 	
    $originalFilename = [System.IO.Path]::GetFileName($path);
    $originalFilename = Encode-ServiceMessageValue($originalFilename);
    $path = Encode-ServiceMessageValue($path)

	Write-Output ""##octopus[createArtifact path='$path' originalFilename='$originalFilename']""
}

$ErrorActionPreference = ""Stop""";

        string GetProxyConfigurationScript()
        {
            if (string.IsNullOrWhiteSpace(proxyConfiguration.CustomProxyUsername))
            {
                return "[System.Net.WebRequest]::DefaultWebProxy.Credentials = [System.Net.CredentialCache]::DefaultCredentials";
            }
            
            return "[System.Net.WebRequest]::DefaultWebProxy.Credentials = new-object System.Net.NetworkCredential(" + EncodeValue(proxyConfiguration.CustomProxyUsername) + ", " + EncodeValue(proxyConfiguration.CustomProxyPassword) + ")";
        }
    }
}