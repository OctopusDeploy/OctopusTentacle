using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Shared.Activities;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Transforms;
using Octopus.Shared.Util;

namespace Octopus.Shared.Conventions.Implementations
{
    public class XmlConfigTransformConvention : IInstallationConvention 
    {
        public IOctopusFileSystem FileSystem { get; set; }
        
        public int Priority
        {
            get { return ConventionPriority.ConfigTransforms; }
        }

        public string FriendlyName { get { return "XML Transformation"; } }

        public void Install(ConventionContext context)
        {
            context.Log.InfoFormat("Looking for any configuration transformation files");
            var configs = FileSystem.EnumerateFilesRecursively(context.PackageContentsDirectoryPath, "*.config");
            var environment = context.Variables.GetValue(SpecialVariables.Environment.Name);

            foreach (var config in configs)
            {
                ApplyConfigTransforms(config, "Release", context);

                if (!string.IsNullOrWhiteSpace(environment))
                {
                    ApplyConfigTransforms(config, environment, context);
                }
            }
        }

        void ApplyConfigTransforms(string sourceFile, string suffix, ConventionContext context)
        {
            var transformFile = Path.ChangeExtension(sourceFile, suffix + ".config");
            if (!FileSystem.FileExists(transformFile))
                return;

            if (string.Equals(sourceFile, transformFile, StringComparison.InvariantCultureIgnoreCase))
                return;

            // Parameters support could be added by using this code:
            // http://ctt.codeplex.com/SourceControl/changeset/view/9525#5293

            var task = new TransformationTask(sourceFile, transformFile, context.Log);
            task.SetParameters(new Dictionary<string, string>());
            var result = task.Execute(sourceFile, false);

            if (result == TransformResult.Failed || result == TransformResult.SuccessWithErrors)
            {
                if (!context.Variables.GetFlag(SpecialVariables.IgnoreConfigTransformationErrors, false))
                {
                    throw new ActivityFailedException("One or more errors were encountered when applying the XML configuration transformation file: " + transformFile + ". View the deployment log for more details, or set the special variable " + SpecialVariables.IgnoreConfigTransformationErrors + " to True to ignore this error.");                
                }
            }

            if (result == TransformResult.SuccessWithWarnings)
            {
                if (context.Variables.GetFlag(SpecialVariables.TreatWarningsAsErrors, false))
                {
                    throw new ActivityFailedException("One or more warnings were encountered when applying the XML configuration transformation file: " + transformFile + ". View the deployment log for more details, or set the special variable " + SpecialVariables.IgnoreConfigTransformationErrors + " to True to ignore this error.");                
                }
            }
        }
    }
}