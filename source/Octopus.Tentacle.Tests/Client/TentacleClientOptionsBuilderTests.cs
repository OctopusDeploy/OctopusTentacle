using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Client
{
    [TestFixture]
    public class TentacleClientOptionsBuilderTests
    {
        [Test]
        public void CannotDisableScriptServiceV1()
        {
            // Arrange
            var builder = new TentacleClientOptionsBuilder();

            // Act + Assert
            Action x = () => builder.DisableScriptService(nameof(IScriptService));

            x.Should().Throw<ArgumentException>();
        }
    }
}