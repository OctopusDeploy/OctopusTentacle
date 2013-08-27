using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Octopus.Platform.Deployment;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Util;
using Octopus.Platform.Variables;
using Octopus.Shared.Activities;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Transforms;

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

        public void Install(IConventionContext context)
        {
            if (context.Variables.GetFlag(SpecialVariables.Step.Package.AutomaticallyRunConfigurationTransformationFiles, true) == false)
            {
                return;
            }

            context.Log.InfoFormat("Looking for any configuration transformation files");
            var configs = FileSystem.EnumerateFilesRecursively(context.PackageContentsDirectoryPath, "*.config");
            var environment = context.Variables.Get(SpecialVariables.Environment.Name);

            foreach (var config in configs)
            {
                var alreadyRun = new HashSet<string>();
                ApplyConfigTransforms(config, "Release", context, alreadyRun);

                if (!string.IsNullOrWhiteSpace(environment))
                {
                    ApplyConfigTransforms(config, environment, context, alreadyRun);
                }

                foreach (var suffix in GetSuffixes(context.Variables.Get(SpecialVariables.Step.Package.AdditionalXmlConfigurationTransforms)))
                {
                    ApplyConfigTransforms(config, suffix, context, alreadyRun);
                }
            }
        }

        IEnumerable<string> GetSuffixes(string suffixes)
        {
            if (string.IsNullOrWhiteSpace(suffixes))
                return new string[0];

            return suffixes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        }

        void ApplyConfigTransforms(string sourceFile, string suffix, IConventionContext context, HashSet<string> alreadyRun)
        {
            if (!suffix.EndsWith(".config", StringComparison.OrdinalIgnoreCase))
            {
                suffix += ".config";
            }

            var transformFile = Path.ChangeExtension(sourceFile, suffix);
            if (!FileSystem.FileExists(transformFile))
                return;

            if (string.Equals(sourceFile, transformFile, StringComparison.InvariantCultureIgnoreCase))
                return;

            if (alreadyRun.Contains(transformFile))
                return;

            alreadyRun.Add(transformFile);

            // Parameters support could be added by using this code:
            // http://ctt.codeplex.com/SourceControl/changeset/view/9525#5293

            var task = new TransformationTask(sourceFile, transformFile, context.Log);
            task.SetParameters(new Dictionary<string, string>());
            var result = task.Execute(sourceFile, false);

            if (result == TransformResult.Failed || result == TransformResult.SuccessWithErrors)
            {
                if (!context.Variables.GetFlag(SpecialVariables.Step.Package.IgnoreConfigTranformationErrors, false))
                {
                    throw new ActivityFailedException("One or more errors were encountered when applying the XML configuration transformation file: " + transformFile + ". View the deployment log for more details, or set the special variable " + SpecialVariables.Step.Package.IgnoreConfigTranformationErrors + " to True to ignore this error.");                
                }
            }

            if (result == TransformResult.SuccessWithWarnings)
            {
                if (context.Variables.GetFlag(SpecialVariables.TreatWarningsAsErrors, false))
                {
                    throw new ActivityFailedException("One or more warnings were encountered when applying the XML configuration transformation file: " + transformFile + ". View the deployment log for more details, or set the special variable " + SpecialVariables.Step.Package.IgnoreConfigTranformationErrors + " to True to ignore this error.");                
                }
            }
        }
    }
}