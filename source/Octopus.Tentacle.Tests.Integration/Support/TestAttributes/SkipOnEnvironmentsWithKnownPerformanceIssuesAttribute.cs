using System;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SkipOnEnvironmentsWithKnownPerformanceIssuesAttribute : NUnitAttribute, IApplyToTest
    {
        string Reason { get; }

        public SkipOnEnvironmentsWithKnownPerformanceIssuesAttribute(string reason)
        {
            Reason = reason;
        }
        
        public void ApplyToTest(Test test)
        {
            if (test.RunState == RunState.NotRunnable || test.RunState == RunState.Ignored)
                return;

            if (bool.TryParse(Environment.GetEnvironmentVariable("Has_Known_Performance_Issues"), out _))
            {
                test.RunState = RunState.Skipped;
                test.Properties.Add("_SKIPREASON", $"This test only runs on environments without performance issues because {Reason}");
            }
        }
    }
}