using System.Collections.Generic;
using FluentValidation;
using Octopus.Diagnostics;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Util;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;

namespace Octopus.Manager.Tentacle.DeleteWizard
{
    public class DeleteWizardModel : ViewModel, IScriptableViewModel
    {
        public DeleteWizardModel(ApplicationName application)
        {
            InstanceName = ApplicationInstanceRecord.GetDefaultInstance(application);
            Executable = CommandLine.PathToTentacleExe();
            ApplicationName = "Tentacle";
            Validator = CreateValidator();
        }

        public string InstanceName { get; set; }
        public string Executable { get; set; }
        public string ApplicationName { get; set; }

        public IEnumerable<CommandLineInvocation> GenerateScript()
        {
            yield return CliBuilder.ForTool(Executable, "service", InstanceName).Flag("stop").Flag("uninstall").Build();
            yield return CliBuilder.ForTool(Executable, "delete-instance", InstanceName).Build();
        }

        public IEnumerable<CommandLineInvocation> GenerateRollbackScript()
        {
            yield break;
        }

        static IValidator CreateValidator()
        {
            var validator = new InlineValidator<DeleteWizardModel>();

            return validator;
        }

        public void ContributeSensitiveValues(ILog log)
        {
            // no sensitive values to contribute
        }
    }
}