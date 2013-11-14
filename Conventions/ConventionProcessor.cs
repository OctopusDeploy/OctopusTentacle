using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Platform.Deployment;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Octopus.Platform.Variables;

namespace Octopus.Shared.Conventions
{
    public class ConventionProcessor : IConventionProcessor
    {
        readonly IEnumerable<IConvention> conventions;

        public ConventionProcessor(IEnumerable<IConvention> conventions)
        {
            this.conventions = conventions;
        }

        public void RunConventions(IConventionContext context)
        {
            EvaluateVariables(context, context.Log);

            try
            {
                // Now run the "conventions", for example: Deploy.ps1 scripts, XML configuration, and so on
                RunInstallConventions(context);

                // Run cleanup for rollback conventions, for example: delete DeployFailed.ps1 script
                RunRollbackCleanup(context);
            }
            catch (Exception ex)
            {
                context.Log.Error("Error running conventions; running rollback conventions...");

                ex = ex.UnpackFromContainers();

                // Not desirable to do this by default in UnpackFromContainers().
                while (ex is ControlledFailureException && ex.InnerException != null)
                    ex = ex.InnerException;

                context.Variables.Set(SpecialVariables.LastError, ex.ToString());
                context.Variables.Set(SpecialVariables.LastErrorMessage, ex.Message);

                // Rollback conventions include tasks like DeployFailed.ps1
                RunRollbackConventions(context);

                // Run cleanup for rollback conventions, for example: delete DeployFailed.ps1 script
                RunRollbackCleanup(context);

                throw;
            }
        }

        void RunInstallConventions(IConventionContext context)
        {
            Run<IInstallationConvention>(context, (c, ctx) =>
            {
                if (!ctx.Variables.GetFlag(SpecialVariables.Action.SkipRemainingConventions, false))
                    c.Install(ctx);
            });
        }

        void RunRollbackConventions(IConventionContext context)
        {
            Run<IRollbackConvention>(context, (c, ctx) => c.Rollback(ctx));
        }

        void RunRollbackCleanup(IConventionContext context)
        {
            Run<IRollbackConvention>(context, (c, ctx) => c.Cleanup(ctx));
        }

        void Run<TConvention>(IConventionContext context, Action<TConvention, IConventionContext> conventionCallback) where TConvention : IConvention
        {
            var conventionsToRun = 
                conventions.OfType<TConvention>()
                .OrderBy(p => p.Priority)
                .ToList();

            foreach (var convention in conventionsToRun)
            {
                var childContext = context.ScopeTo(convention);
                try
                {
                    conventionCallback(convention, childContext);
                    childContext.Log.EndOperation();
                }
                catch (ControlledFailureException ex)
                {
                    Log.Octopus().Verbose(ex);
                    childContext.Log.Fatal(ex.Message);
                    throw new ControlledFailureException("A convention could not be successfully applied.", ex);
                }
                catch (Exception ex)
                {
                    childContext.Log.Fatal(ex);
                    // While not strictly a "controlled" failure, we're certain to have logged the
                    // issue at this point.
                    throw new ControlledFailureException("An error occurred in a convention.", ex);
                }
            }
        }

        static void EvaluateVariables(IConventionContext context, ILog log)
        {
            context.Variables.Set(SpecialVariables.OriginalPackageDirectoryPath, context.PackageContentsDirectoryPath);

            if (context.Variables.GetFlag(SpecialVariables.PrintVariables, false))
            {
                PrintVariables("The following variables are available:", context.Variables, log);
            }

            if (!context.Variables.GetFlag(SpecialVariables.NoVariableTokenReplacement, false))
            {
                new VariableEvaluator().Evaluate(context.Variables);

                if (context.Variables.GetFlag(SpecialVariables.PrintEvaluatedVariables, false))
                {
                    log.Verbose("Variables have been evaluated.");
                    PrintVariables("The following evaluated variables are available:", context.Variables, log);
                }
            }
        }

        static void PrintVariables(string message, VariableDictionary variables, ILog log)
        {
            log.Verbose(message);

            foreach (var variable in variables.AsList().OrderBy(v => v.Name))
            {
                if (variable.IsSensitive)
                    log.VerboseFormat(" - [{0}] = ********", variable.Name);
                else
                    log.VerboseFormat(" - [{0}] = '{1}'", variable.Name, variable.Value);
            }
        }
    }
}