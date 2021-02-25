using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Octopus.Shared.Util;

namespace Octopus.Tentacle.Tests.Support
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class WindowsTestAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
            if (test.RunState == RunState.NotRunnable || test.RunState == RunState.Ignored)
                return;

            if (!PlatformDetection.IsRunningOnWindows)
            {
                test.RunState = RunState.Skipped;
                test.Properties.Add("_SKIPREASON", "This test only runs on Windows");
            }
        }
    }
}