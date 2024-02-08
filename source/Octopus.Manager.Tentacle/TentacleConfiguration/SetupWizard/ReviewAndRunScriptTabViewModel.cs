using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using FluentValidation;
using Octopus.Diagnostics;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard
{
    public class ReviewAndRunScriptTabViewModel : ViewModel, IScriptableViewModel
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly IScriptableViewModel wizardModel;
        TextBoxLogger logger;

        public ReviewAndRunScriptTabViewModel(IScriptableViewModel wizardModel, IComponentContext container)
        {
            this.wizardModel = wizardModel;
            InstanceName = wizardModel.InstanceName;
            Executable = CommandLine.PathToTentacleExe();
            Validator = CreateValidator();
            commandLineRunner = container.Resolve<ICommandLineRunner>();
        }
        
        public string InstanceName { get; }
        
        public string Executable { get; set; }
        
        public async Task<bool> GenerateAndExecuteScript()
        {
            var success = false;
            try
            {
                var script = GenerateScript();
                ContributeSensitiveValues(logger);
                success = commandLineRunner.Execute(script, logger);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
            finally
            {
                if (!success)
                {
                    Rollback();
                }
            }

            await Task.CompletedTask;
            return success;
        }
            
        void Rollback()
        {
            try
            {
                var script = GenerateRollbackScript();
                commandLineRunner.Execute(script, logger);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public void ContributeSensitiveValues(ILog log)
        {
            wizardModel.ContributeSensitiveValues(log);
        }
        
        public IEnumerable<CommandLineInvocation> GenerateScript()
        {
            return wizardModel.GenerateScript();
        }

        public IEnumerable<CommandLineInvocation> GenerateRollbackScript()
        {
            return wizardModel.GenerateRollbackScript();
        }
        
        IValidator CreateValidator()
        {
            var validator = new InlineValidator<ReviewAndRunScriptTabViewModel>();
            return validator;
        }

        public void SetLogger(TextBoxLogger newLogger)
        {
            logger = newLogger;
        }
    }
}
