using System;

namespace Octopus.Tentacle.Util
{
    public class TeamCityDetection
    {
        private static Lazy<bool> IsRunningInTeamCityLazy = new Lazy<bool>(() =>
        {
            // Under linux we don't have the team city environment variables
            if (typeof(TeamCityDetection).Assembly.Location.Contains("TeamCity"))
            {
                return true;
            }

            // Under windows we do.
            var teamcityenvvars = new String[] {"TEAMCITY_VERSION", "TEAMCITY_BUILD_ID"};
            foreach (var s in teamcityenvvars)
            {
                var environmentVariableValue = Environment.GetEnvironmentVariable(s);
                if (!string.IsNullOrEmpty(environmentVariableValue)) return true;
            }

            return false;
        });
        
        public static bool IsRunningInTeamCity()
        {
            return IsRunningInTeamCityLazy.Value;
        }
    }
}
