using System;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Integration.PowerShell
{
    public class PowerShellEngineSelector : IPowerShell
    {
        public PowerShellExecutionResult Execute(PowerShellArguments arguments)
        {
            var runner = SelectEngine(arguments);
            return runner.Execute(arguments);
        }

        static IPowerShell SelectEngine(PowerShellArguments arguments)
        {
            string legacyString;
            bool legacyValue;
            if (arguments.Variables.TryGetValue(SpecialVariables.UseLegacyPowerShellEngine, out legacyString)
                && Boolean.TryParse(legacyString, out legacyValue)
                && legacyValue)
            {
                return new HostedPowerShellRunner();
            }
            
            return new FileBasedPowerShellRunner();
        }
    }
}