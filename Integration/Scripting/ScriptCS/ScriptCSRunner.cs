using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Util;

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
            if (!RuntimeUtility.IsNet45OrNewer())
            {
                throw new Exception("ScriptCS files require the Roslyn CTP, which requires .NET framework 4.5");
            }

            var configurationFile = PrepareConfigurationFile(arguments);
            var bootstrapFile = PrepareBootstrapFile(arguments, configurationFile);

            try
            {
                var commandArguments = new StringBuilder();
                commandArguments.AppendFormat("\"{0}\"", bootstrapFile);

                var filter = new ScriptExecutionOutputFilter(arguments.Log);

                var errorWritten = false;
                var exit = SilentProcessRunner.ExecuteCommand(GetScriptCsPath(), commandArguments.ToString(), arguments.WorkingDirectory,
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
                File.Delete(configurationFile);
            }
        }

        static string GetScriptCsPath()
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            var scriptCsFolder = Path.Combine(Path.GetDirectoryName(typeof(ScriptCSRunner).Assembly.FullLocalPath()), "ScriptCS");
            return Path.Combine(scriptCsFolder, "scriptcs.exe");
        }

        // We create a temporary file to invoke the PowerShell script with the variables loaded
        static string PrepareBootstrapFile(ScriptArguments arguments, string configurationFile)
        {
            var bootstrapFile = Path.Combine(arguments.WorkingDirectory, "Bootstrap." + Guid.NewGuid() + ".csx");

            using (var writer = new StreamWriter(bootstrapFile))
            {
                writer.WriteLine("#load \"" + configurationFile.Replace("\\", "\\\\") + "\"");
                writer.WriteLine("#load \"" + arguments.ScriptFilePath.Replace("\\", "\\\\") + "\"");
                writer.Flush();
            }

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return bootstrapFile;
        }

        // We create a temporary file to invoke the PowerShell script with the variables loaded
        static string PrepareConfigurationFile(ScriptArguments arguments)
        {
            var bootstrapFile = Path.Combine(arguments.WorkingDirectory, "Configure." + Guid.NewGuid() + ".csx");

            using (var writer = new StreamWriter(bootstrapFile))
            {
                writer.WriteLine("#r \"System.Security, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a\"");
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

            File.SetAttributes(bootstrapFile, FileAttributes.Hidden);
            return bootstrapFile;
        }

        static void WriteVariableDictionary(ScriptArguments arguments, StreamWriter writer)
        {
            foreach (var variable in arguments.Variables.AsList())
            {
                writer.WriteLine("    this[" + EncodeValue(variable.Name) + "] = " + EncodeValue(variable.Value, variable.IsSensitive) + ";");
            }
        }

        static string EncodeValue(string value, bool isSensitive = false)
        {
            if (value == null)
                return "null";

            var bytes = Encoding.UTF8.GetBytes(value);
            string decryptBytesStart = "", decryptBytesEnd = "";
            if (isSensitive)
            {
                bytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                decryptBytesStart = "System.Security.Cryptography.ProtectedData.Unprotect(";
                decryptBytesEnd = ", null, System.Security.Cryptography.DataProtectionScope.CurrentUser)";
            }

            return "System.Text.Encoding.UTF8.GetString(" +
                       decryptBytesStart +
                           "Convert.FromBase64String(\"" +
                               Convert.ToBase64String(bytes) + 
                           "\")" +
                       decryptBytesEnd +
                   ")";
        }

        const string EmbeddedFunctions =
@"  static string EncodeServiceMessageValue(string value)
  {
	var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
	return Convert.ToBase64String(valueBytes);
  }

  public static void SetVariable(string name, string value) 
  { 	
    name = EncodeServiceMessageValue(name);
    value = EncodeServiceMessageValue(value);

	Console.WriteLine(""##octopus[setVariable name='{0}' value='{1}']"", name, value);
  }

  public static void CreateArtifact(string path) 
  {
    var originalFilename = System.IO.Path.GetFileName(path); 
    originalFilename = EncodeServiceMessageValue(originalFilename);	

    path = System.IO.Path.GetFullPath(path);
    path = EncodeServiceMessageValue(path);

	Console.WriteLine(""##octopus[createArtifact path='{0}' originalFilename='{1}']"", path, originalFilename);
  }";
    }
}