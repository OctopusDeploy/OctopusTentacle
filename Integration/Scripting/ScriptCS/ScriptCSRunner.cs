using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Octopus.Shared.ServiceMessages;
using Octopus.Shared.Util;

namespace Octopus.Shared.Integration.Scripting.ScriptCS
{
    public class ScriptCSRunner : IScriptRunner
    {
        public string[] GetSupportedExtensions()
        {
            return new[] {"csx"};
        }

        public ScriptExecutionResult Execute(ScriptArguments arguments)
        {
            var configurationFile = PrepareConfigurationFile(arguments);
            var bootstrapFile = PrepareBootstrapFile(arguments, configurationFile);

            var outputVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var commandArguments = new StringBuilder();
                commandArguments.AppendFormat("-s \"{0}\"", bootstrapFile);

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
                var exit = SilentProcessRunner.ExecuteCommand(GetScriptCsPath(), commandArguments.ToString(), arguments.WorkingDirectory,
                    output => parser.Append(output + Environment.NewLine),
                    error => { 
                        arguments.OutputStream.OnWritten("ERROR: " + error);
                        errorWritten = true;
                    });

                if (errorWritten && exit == 0)
                {
                    exit = -12;
                }

                return new ScriptExecutionResult(exit, outputVariables);
            }
            finally
            {
                File.Delete(bootstrapFile);
                File.Delete(configurationFile);
            }
        }

        static string GetScriptCsPath()
        {
            var scriptCsFolder = Path.Combine(Path.GetDirectoryName(typeof(ScriptCSRunner).Assembly.FullLocalPath()), "ScriptCS");
            return Path.Combine(scriptCsFolder, "scriptcs.exe");
        }

        // We create a temporary file to invoke the PowerShell script with the variables loaded
        static string PrepareBootstrapFile(ScriptArguments arguments, string configurationFile)
        {
            var bootstrapFile = Path.Combine(arguments.WorkingDirectory, "Bootstrap." + Guid.NewGuid() + ".csx");
            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);

            using (var writer = new StreamWriter(bootstrapFile))
            {
                writer.WriteLine("#load \"" + configurationFile.Replace("\\", "\\\\") + "\"");
                writer.WriteLine("#load \"" + arguments.ScriptFilePath.Replace("\\", "\\\\") + "\"");
                writer.Flush();
            }
            
            return bootstrapFile;
        }

        // We create a temporary file to invoke the PowerShell script with the variables loaded
        static string PrepareConfigurationFile(ScriptArguments arguments)
        {
            var bootstrapFile = Path.Combine(arguments.WorkingDirectory, "Configure." + Guid.NewGuid() + ".csx");
            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);

            using (var writer = new StreamWriter(bootstrapFile))
            {
                writer.WriteLine("using System;");
                writer.WriteLine("public static class Octopus");
                writer.WriteLine("{");
                writer.WriteLine("  public static readonly OctopusParametersDictionary Parameters = new OctopusParametersDictionary();");
                writer.WriteLine();
                writer.WriteLine("  public class OctopusParametersDictionary : System.Collections.Generic.Dictionary<string,string>");
                writer.WriteLine("  {");
                writer.WriteLine("    public OctopusParametersDictionary() : base(System.StringComparer.OrdinalIgnoreCase)");
                writer.WriteLine("    {");
                WriteVariableDictionary(arguments, writer);
                writer.WriteLine("    }");
                writer.WriteLine("  }");
                writer.WriteLine();
                writer.WriteLine(EmbeddedFunctions);
                writer.WriteLine("}");

                writer.WriteLine();

                writer.Flush();
            }

            return bootstrapFile;
        }

        static void WriteVariableDictionary(ScriptArguments arguments, StreamWriter writer)
        {
            foreach (var variable in arguments.Variables)
            {
                writer.WriteLine("    this[" + EncodeValue(variable.Key) + "] = " + EncodeValue(variable.Value) + ";");
            }
        }

        static string EncodeValue(string value)
        {
            if (value == null)
                return "null";

            return "System.Text.Encoding.UTF8.GetString(Convert.FromBase64String( \"" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)) + "\" ) )";
        }

        static bool IsValidScriptIdentifierChar(char c)
        {
            return c == '_' || char.IsLetterOrDigit(c);
        }

        const string EmbeddedFunctions =
@"  static string EncodeServiceMessageValue(string value)
  {
	var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
	return Convert.ToBase64String(valueBytes);
  }

  public static void SetOctopusVariable(string name, string value) 
  { 	
    name = EncodeServiceMessageValue(name);
    value = EncodeServiceMessageValue(value);

	Console.WriteLine(""##octopus[setVariable name='$name' value='$value']"");
  }";
    }
}