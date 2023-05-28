using System;
using System.Threading;
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
            ThreadPool.SetMaxThreads(2000, 2000);
            ThreadPool.SetMinThreads(2000, 2000);
            // TODO don't actually bring this in.
            return ParallelScope.All;
            // if (TentacleExeFinder.IsRunningInTeamCity()) return ParallelScope.Default;
            // var var  =Environment.GetEnvironmentVariable("RunTestsInParallel") ?? "false";
            // if (var.Equals("true"))
            // {
            //     ThreadPool.SetMaxThreads(2000, 2000);
            //     ThreadPool.SetMinThreads(2000, 2000);
            //     return ParallelScope.All;
            // }
            // return ParallelScope.Default;
        }
    }
}