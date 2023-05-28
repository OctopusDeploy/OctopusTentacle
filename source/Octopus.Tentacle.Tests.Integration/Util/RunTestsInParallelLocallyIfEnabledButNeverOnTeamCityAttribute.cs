using System;
using System.Threading;
using NUnit.Framework;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    // TODO rename this (when it will not cause a painful merge conflict)
    public class RunTestsInParallelLocallyIfEnabledButNeverOnTeamCityAttribute : ParallelizableAttribute
    {
        public RunTestsInParallelLocallyIfEnabledButNeverOnTeamCityAttribute() : base(ScopeFromEnv())
        {
        }

        public static ParallelScope ScopeFromEnv()
        {
            ThreadPool.SetMaxThreads(2000, 2000);
            ThreadPool.SetMinThreads(2000, 2000);
            return ParallelScope.All;
        }
    }
}