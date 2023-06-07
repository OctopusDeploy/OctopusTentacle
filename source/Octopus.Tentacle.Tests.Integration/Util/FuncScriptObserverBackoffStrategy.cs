using System;
using Octopus.Tentacle.Client.Scripts;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class FuncScriptObserverBackoffStrategy : IScriptObserverBackoffStrategy
    {
        private Func<int, TimeSpan> GetBackoffFunc;

        public FuncScriptObserverBackoffStrategy(Func<int, TimeSpan> getBackoffFunc)
        {
            GetBackoffFunc = getBackoffFunc;
        }

        public TimeSpan GetBackoff(int iteration)
        {
            return GetBackoffFunc(iteration);
        }
    }
}