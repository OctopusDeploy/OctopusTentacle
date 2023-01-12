using System;

namespace Octopus.Tentacle.Tests
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