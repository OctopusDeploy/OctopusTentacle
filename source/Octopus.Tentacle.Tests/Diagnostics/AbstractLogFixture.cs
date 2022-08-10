using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Tests.Support;

namespace Octopus.Tentacle.Tests.Diagnostics
{
    [TestFixture]
    public class AbstractLogFixture
    {
        [Test]
        public void InvalidFormatStringsDoNotResultInExceptions()
        {
            var log = new InMemoryLog();
            log.InfoFormat("I'm {0} this parameter: {1}", "missing");
            var text = log.GetLog();

            text.Should().Contain("I'm {0} this parameter: {1}");
            text.Should().Contain("missing");
            text.Should().Contain("=> Index");
        }
    }
}