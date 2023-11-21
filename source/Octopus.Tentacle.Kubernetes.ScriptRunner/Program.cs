using System.CommandLine;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes.ScriptRunner.Commands;
using Octopus.Tentacle.Kubernetes.ScriptRunner.Diagnostics;

Log.Appenders.Add(new ConsoleLogAppender());

var executeScriptCommand = new ExecuteScriptCommand();

return await executeScriptCommand.InvokeAsync(args);