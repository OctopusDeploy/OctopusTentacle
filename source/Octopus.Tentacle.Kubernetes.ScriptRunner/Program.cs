// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes.ScriptRunner.Commands;

//Add the NLog appender (for use with the SystemLog)
Log.Appenders.Add(new NLogAppender());

var executeScriptCommand = new ExecuteScriptCommand();

return await executeScriptCommand.InvokeAsync(args);