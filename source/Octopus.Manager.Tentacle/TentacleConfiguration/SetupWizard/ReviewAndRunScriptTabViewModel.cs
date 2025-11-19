using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard
{
    public class ReviewAndRunScriptTabViewModel : ViewModel, IScriptableViewModel
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly Func<Task> onScriptSucceeded;
        readonly Func<Task> onScriptFailed;
        readonly IScriptableViewModel wizardModel;
        SystemLog logger;

        public ReviewAndRunScriptTabViewModel(
            IScriptableViewModel wizardModel,
            ICommandLineRunner commandLineRunner,
            Func<Task> onScriptSucceeded = null,
            Func<Task> onScriptFailed = null
            )
        {
            this.wizardModel = wizardModel;
            this.commandLineRunner = commandLineRunner;
            this.onScriptSucceeded = onScriptSucceeded;
            this.onScriptFailed = onScriptFailed;
            
            InstanceName = wizardModel.InstanceName;
            Executable = CommandLine.PathToTentacleExe();
            Validator = CreateValidator();
        }
        
        public string InstanceName { get; }
        
        public string Executable { get; }
        
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
                if (success)
                {
                    if (onScriptSucceeded != null)
                    {
                        await onScriptSucceeded();
                    }
                }
                else
                {
                    Rollback();
                    if (onScriptFailed != null)
                    {
                        await onScriptFailed();
                    }
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

        // TODO: Remove this workaround
        // This only exists so that a TextBoxLogger, a UI component,
        // can be set from the view.
        // We should change this so that the model doesn't depend
        // on a UI component.
        public void SetLogger(SystemLog newLogger)
        {
            logger = newLogger;
        }
    }
}
