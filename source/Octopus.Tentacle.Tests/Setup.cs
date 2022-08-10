using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace Octopus.Tentacle.Tests
{
    [SetUpFixture]
    public class Setup
    {
        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-AU");
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-AU");
        }
    }
}