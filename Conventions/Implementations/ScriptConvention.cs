using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Util;
using Octopus.Platform.Variables;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Scripting;

namespace Octopus.Shared.Conventions.Implementations
{
    public abstract class ScriptConvention : IConvention
    {
        public IOctopusFileSystem FileSystem { get; set; }
        public IScriptRunner ScriptRunner { get; set; }

        public abstract int Priority { get; }

        public abstract string FriendlyName { get; }

        protected void RunScript(string scriptName, IConventionContext context)
        {
            var scripts = FindScripts(scriptName, context);

            foreach (var script in scripts)
            {
                context.Log.VerboseFormat("Script: {0}", script);

                var arguments = new ScriptArguments();
                arguments.ScriptFilePath = script;
                arguments.WorkingDirectory = context.PackageContentsDirectoryPath;
                arguments.Variables = context.Variables.AsDictionary();
                arguments.OutputStream.Written += context.Log.Info;
                
                var result = ScriptRunner.Execute(arguments);
                foreach (var createdArtifact in result.CreatedArtifacts)
                {
                    context.AddCreatedArtifact(createdArtifact);
                }

                arguments.OutputStream.Written -= context.Log.Info;

                foreach (var outputVariable in result.OutputVariables)
                {
                    context.Variables.Set(outputVariable.Key, outputVariable.Value);
                }

                if (result.ExitCode == 0 && result.StdErrorWritten)
                {
                    context.Log.WarnFormat("The script returned an exit code of 0, but output was written to the error output stream. Please investigate any errors in the script output above.");
                    if (context.Variables.GetFlag(SpecialVariables.TreatWarningsAsErrors, false))
                        throw new ScriptFailureException("One or more errors were encountered when running the script.");
                }

                if (result.ExitCode != 0)
                {
                    throw new ScriptFailureException(string.Format("Script '{0}' returned non-zero exit code: {1}. Deployment terminated.", script, result.ExitCode));
                }
            }
        }

        protected void DeleteScript(string scriptName, IConventionContext context)
        {
            var scripts = FindScripts(scriptName, context);

            foreach (var script in scripts)
            {
                FileSystem.DeleteFile(script, DeletionOptions.TryThreeTimesIgnoreFailure);
            }
        }

        protected IEnumerable<string> FindScripts(string scriptName, IConventionContext context)
        {
            var scripts = FileSystem.EnumerateFilesRecursively(context.StagingDirectoryPath, ScriptRunner.GetSupportedExtensions().Select(e => "*." + e).ToArray()).ToArray();
            
            scripts = scripts.Where(s => Path.GetFileNameWithoutExtension(s).Equals(scriptName, StringComparison.InvariantCultureIgnoreCase)).ToArray();

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
    }
}