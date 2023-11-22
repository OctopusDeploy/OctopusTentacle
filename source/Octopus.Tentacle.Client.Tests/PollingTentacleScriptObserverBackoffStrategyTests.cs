using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client.Scripts;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class PollingTentacleScriptObserverBackoffStrategyTests
    {
        [Test]
        public void BacksOffCorrectly()
        {
            // Arrange
            var testIteration = 15;
            var expectedBackoffsInMiliseconds = new[]
            {
                300.0,
                420.0,
                588.0,
                823.0,
                1152.0,
                1613.0,
                2259.0,
                3162.0,
                4427.0,
                5000.0,
                5000.0,
                5000.0,
                5000.0,
                5000.0,
                5000.0
            };

            // Act
            var results = new List<double>();
            var pollingTentacleScriptObserverBackoffStrategy = new PollingTentacleScriptObserverBackoffStrategy();

            for (var i = 0; i < testIteration; i++)
            {
                var result = pollingTentacleScriptObserverBackoffStrategy.GetBackoff(i);
                results.Add(result.TotalMilliseconds);
            }

            // Assert
            results.Should().BeEquivalentTo(expectedBackoffsInMiliseconds);
        }
    }
}