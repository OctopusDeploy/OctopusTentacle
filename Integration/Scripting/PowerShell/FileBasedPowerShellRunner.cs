using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Util;

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
                var escapedBootstrapFile = bootstrapFile.Replace("'", "''");
                commandArguments.AppendFormat("-Command \"& {{. '{0}'; if ((test-path variable:global:lastexitcode)) {{ exit $LastExitCode }}}}\"", escapedBootstrapFile);

                var filter = new ScriptExecutionOutputFilter(arguments.Log);

                var errorWritten = false;
                var exit = SilentProcessRunner.ExecuteCommand("powershell.exe", commandArguments.ToString(), arguments.WorkingDirectory,
                    filter.WriteLine,
                    error => {
                        if (!string.IsNullOrWhiteSpace(error))
                            arguments.Log.Error(error);
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
                writer.WriteLine("## Dependencies:");

                writer.WriteLine("Add-Type -AssemblyName System.Security");

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

            // File.Copy(bootstrapFile, "C:\\BS-" + DateTime.Now.Ticks + ".ps1");

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return bootstrapFile;
        }

        static void WriteVariableDictionary(ScriptArguments arguments, StreamWriter writer)
        {
            writer.WriteLine("$OctopusParameters = New-Object 'System.Collections.Generic.Dictionary[String,String]' (,[System.StringComparer]::OrdinalIgnoreCase)");
            foreach (var variable in arguments.Variables.AsList())
            {
                writer.WriteLine("$OctopusParameters[" + EncodeValue(variable.Name) + "] = " + EncodeValue(variable.Value, variable.IsSensitive));
            }
        }

        static void WriteLocalVariables(ScriptArguments arguments, StreamWriter writer)
        {
            foreach (var variable in arguments.Variables.AsList())
            {
                // This is the way we used to fix up the identifiers - people might still rely on this behavior
                var legacyKey = new string(variable.Name.Where(char.IsLetterOrDigit).ToArray());

                // This is the way we should have done it
                var smartKey = new string(variable.Name.Where(IsValidPowerShellIdentifierChar).ToArray());

                if (legacyKey != smartKey)
                {
                    writer.WriteLine("$" + legacyKey + " = " + EncodeValue(variable.Value, variable.IsSensitive));
                }

                writer.WriteLine("$" + smartKey + " = " + EncodeValue(variable.Value, variable.IsSensitive));
            }
        }

        static string EncodeValue(string value, bool isSensitive = false)
        {
            if (value == null)
                return "$null";

            var bytes = Encoding.UTF8.GetBytes(value);
            string decryptBytesStart = "", decryptBytesEnd = "";
            if (isSensitive)
            {
                bytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                decryptBytesStart = "[System.Security.Cryptography.ProtectedData]::Unprotect(";
                decryptBytesEnd = ", $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)";
            }

            return "[System.Text.Encoding]::UTF8.GetString(" +
                       decryptBytesStart +
                           "[Convert]::FromBase64String(\"" + 
                               Convert.ToBase64String(bytes) + 
                           "\")" + 
                       decryptBytesEnd + 
                   ")";
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

	Write-Host ""##octopus[setVariable name='$name' value='$value']""
}

function New-OctopusArtifact([string]$path) 
{ 	
    $originalFilename = [System.IO.Path]::GetFileName($path)
    $originalFilename = Encode-ServiceMessageValue($originalFilename)

    $path = [System.IO.Path]::GetFullPath($path)
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