using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Octopus.Tentacle.Tests.Integration.Support.TestAttributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class SyncAndAsyncTestCaseAttribute : TestCaseSourceAttribute
    {
        public SyncAndAsyncTestCaseAttribute(params object[] parameters) :
            base(
                typeof(SyncAndAsyncTestCase),
                nameof(SyncAndAsyncTestCase.GetTestCases),
                new object []{ parameters.ToArray() })
        {
        }

        static class SyncAndAsyncTestCase
        {
            public static IEnumerable GetTestCases(object[] parameters)
            {
                var syncParameterList = new List<object>(parameters) { SyncOrAsyncHalibut.Sync };
                var asyncParameterList = new List<object>(parameters) { SyncOrAsyncHalibut.Async };

                yield return syncParameterList.ToArray();
                yield return asyncParameterList.ToArray();
            }
        }
    }
}
