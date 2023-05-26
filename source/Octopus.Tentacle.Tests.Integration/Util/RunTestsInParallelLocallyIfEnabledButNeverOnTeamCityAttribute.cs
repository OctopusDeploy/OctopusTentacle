using System;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class RunTestsInParallelLocallyIfEnabledButNeverOnTeamCityAttribute : ParallelizableAttribute
    {
        public RunTestsInParallelLocallyIfEnabledButNeverOnTeamCityAttribute() : base(ScopeFromEnv())
        {
        }

        public static ParallelScope ScopeFromEnv()
        {
            if (TentacleExeFinder.IsRunningInTeamCity()) return ParallelScope.Default;
            var var  =Environment.GetEnvironmentVariable("RunTestsInParallel") ?? "false";
            if (var.Equals("true")) return ParallelScope.All;
            return ParallelScope.Default;
        }
    }
}