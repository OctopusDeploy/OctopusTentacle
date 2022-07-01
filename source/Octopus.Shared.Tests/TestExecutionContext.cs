using System;

namespace Octopus.Shared.Tests
{
    public static class TestExecutionContext
    {
        public static bool IsRunningInTeamCity
        {
            get
            {
                var environmentVariableValue = Environment.GetEnvironmentVariable("TEAMCITY_VERSION");
                return !string.IsNullOrEmpty(environmentVariableValue);
            }
        }
    }
}