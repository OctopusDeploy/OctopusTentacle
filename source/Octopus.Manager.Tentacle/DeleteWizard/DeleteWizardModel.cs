using System.Collections.Generic;
using FluentValidation;
using Octopus.Diagnostics;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.DeleteWizard
{
    public class DeleteWizardModel : ShellViewModel, IScriptableViewModel
    {
        public DeleteWizardModel(InstanceSelectionModel instanceSelectionModel)
            : base(instanceSelectionModel)
        {
            InstanceName = instanceSelectionModel.SelectedInstance;
            Executable = CommandLine.PathToTentacleExe();
            ApplicationName = "Tentacle";
            Validator = CreateValidator();
        }

        public string InstanceName { get; }
        public string Executable { get; }
        public string ApplicationName { get; }

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
