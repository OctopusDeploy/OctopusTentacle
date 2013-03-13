using System;

namespace Octopus.Shared.Integration.PowerShell
{
    public interface IPowerShell
    {
        PowerShellExecutionResult Execute(PowerShellArguments arguments);
    }
}