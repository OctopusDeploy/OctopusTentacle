using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Shared.Activities;
using Octopus.Shared.Integration.PowerShell;
using Octopus.Shared.Util;

namespace Octopus.Shared.Conventions
{
    public abstract class PowerShellConvention : IConvention
    {
        public IOctopusFileSystem FileSystem { get; set; }
        public IPowerShell PowerShell { get; set; }

        public abstract int Priority { get; }

        public abstract string FriendlyName { get; }

        protected void RunScript(string scriptName, ConventionContext context)
        {
            var scripts = FindPowerShellScripts(scriptName, context);

            foreach (var script in scripts)
            {
                context.Log.InfoFormat("Calling PowerShell script: '{0}'", script);

                var closure = new LogClosure(context.Log);

                var arguments = new PowerShellArguments();
                arguments.ScriptFilePath = script;
                arguments.WorkingDirectory = context.PackageContentsDirectoryPath;
                arguments.Variables = context.Variables.AsDictionary();
                arguments.OutputStream.Written += closure.AppendLog;
                
                var result = PowerShell.Execute(arguments);

                arguments.OutputStream.Written -= closure.AppendLog;

                context.Log.DebugFormat("Exit code: {0}", result.ExitCode);

                foreach (var outputVariable in result.OutputVariables)
                {
                    context.Variables.Set(outputVariable.Key, outputVariable.Value);
                }

                if (result.ExitCode != 0)
                {
                    throw new ScriptFailureException(string.Format("PowerShell script '{0}' returned non-zero exit code: {1}. Deployment terminated.", script, result.ExitCode));
                }
            }
        }

        protected void DeleteScript(string scriptName, ConventionContext context)
        {
            var scripts = FindPowerShellScripts(scriptName, context);

            foreach (var script in scripts)
            {
                FileSystem.DeleteFile(script, DeletionOptions.TryThreeTimesIgnoreFailure);
            }
        }

        protected IEnumerable<string> FindPowerShellScripts(string scriptName, ConventionContext context)
        {
            var scripts = FileSystem.EnumerateFilesRecursively(context.StagingDirectoryPath, "*.ps1").ToArray();
            
            scripts = scripts.Where(s => Path.GetFileName(s).Equals(scriptName, StringComparison.InvariantCultureIgnoreCase)).ToArray();
            
            if (context.StagingDirectoryPath != context.PackageContentsDirectoryPath)
            {
                var relativePathIndex = context.StagingDirectoryPath.Length;
                if (!context.StagingDirectoryPath.EndsWith("\\"))
                {
                    relativePathIndex++;
                }

                scripts = scripts.Select(s => Path.Combine(context.PackageContentsDirectoryPath, s.Substring(relativePathIndex))).ToArray();
            }
            return scripts;
        }

        class LogClosure
        {
            readonly IActivityLog log;

            public LogClosure(IActivityLog log)
            {
                this.log = log;
            }

            public void AppendLog(string message)
            {
                log.Info(message);
            }
        }
    }
}