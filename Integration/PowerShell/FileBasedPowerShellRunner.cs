using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Octopus.Shared.ServiceMessages;
using Octopus.Shared.Util;

namespace Octopus.Shared.Integration.PowerShell
{
    public class FileBasedPowerShellRunner : IPowerShell
    {
        public PowerShellExecutionResult Execute(PowerShellArguments arguments)
        {
            var bootstrapFile = PrepareBootstrapFile(arguments);


            var outputVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var commandArguments = new StringBuilder();
                commandArguments.Append("-NonInteractive ");
                commandArguments.Append("-NoLogo ");
                commandArguments.Append("-ExecutionPolicy Unrestricted ");
                commandArguments.AppendFormat("-File \"{0}\"", bootstrapFile);

                var parser = new ServiceMessageParser(
                    output => arguments.OutputStream.OnWritten(output),
                    message =>
                    {
                        if (message.Name == ServiceMessageNames.SetVariable.Name)
                        {
                            outputVariables[message.GetValue(ServiceMessageNames.SetVariable.NameAttribute)] = message.GetValue(ServiceMessageNames.SetVariable.ValueAttribute);
                        }
                    });

                var errorWritten = false;
                var exit = SilentProcessRunner.ExecuteCommand("powershell.exe", commandArguments.ToString(), arguments.WorkingDirectory,
                    output => parser.Append(output + Environment.NewLine),
                    error => { 
                        arguments.OutputStream.OnWritten("ERROR: " + error);
                        errorWritten = true;
                    });

                if (errorWritten && exit == 0)
                {
                    exit = -12;
                }

                return new PowerShellExecutionResult(exit, outputVariables);
            }
            finally
            {
                File.Delete(bootstrapFile);
            }
        }

        // We create a temporary file to invoke the PowerShell script with the variables loaded
        static string PrepareBootstrapFile(PowerShellArguments arguments)
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

                writer.WriteLine("## Invoke:");
                

                writer.WriteLine(". \"" + arguments.ScriptFilePath + "\"");
                writer.WriteLine("exit $LastExitCode");
                writer.Flush();
            }
            
            return bootstrapFile;
        }

        static void WriteVariableDictionary(PowerShellArguments arguments, StreamWriter writer)
        {
            writer.WriteLine("$OctopusParameters = New-Object 'System.Collections.Generic.Dictionary[String,String]' (,[System.StringComparer]::OrdinalIgnoreCase)");
            foreach (var variable in arguments.Variables)
            {
                writer.WriteLine("$OctopusParameters[" + EncodeValue(variable.Key) + "] = " + EncodeValue(variable.Value));
            }
        }

        static void WriteLocalVariables(PowerShellArguments arguments, StreamWriter writer)
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

$ErrorActionPreference = ""Stop""";
    }
}