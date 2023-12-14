using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    public static class ScriptExecutionResultExtensionMethods
    {
        public static void LogExecuteScriptOutput(this (ScriptExecutionResult ScriptExecutionResult, List<ProcessOutput> ProcessOutput) result, ILogger logger)
        {
            var scriptOutput = new StringBuilder();

            scriptOutput.AppendLine("");
            scriptOutput.AppendLine("");
            scriptOutput.AppendLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            scriptOutput.AppendLine("~~ Start Execute Script Output ~~");
            scriptOutput.AppendLine("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

            scriptOutput.AppendLine($"ScriptExecutionResult.State: {result.ScriptExecutionResult.State}");
            scriptOutput.AppendLine($"ScriptExecutionResult.ExitCode: {result.ScriptExecutionResult.ExitCode}");
            scriptOutput.AppendLine("");
            
            result.ProcessOutput.ForEach(x => scriptOutput.AppendLine($"{x.Source} {x.Occurred} {x.Text}"));

            scriptOutput.AppendLine($@"~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            scriptOutput.AppendLine($@"~~~ End Execute Script Output ~~~");
            scriptOutput.AppendLine($@"~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

            logger.Information(scriptOutput.ToString());
        }
    }
}
