using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client.Scripts;

namespace Octopus.Tentacle.Tests.Client
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class DefaultScriptObserverBackoffStrategyTests
    {
        [Test]
        public void BacksOffCorrectly()
        {
            // Arrange
            var testIteration = 25;
            var expectedBackoffsInMiliseconds = new[]
            {
                300.0,
                345.0,
                397.0,
                456.0,
                525.0,
                603.0,
                694.0,
                798.0,
                918.0,
                1055.0,
                1214.0,
                1396.0,
                1605.0,
                1846.0,
                2123.0,
                2441.0,
                2807.0,
                3228.0,
                3713.0,
                4270.0,
                4910.0,
                5000.0,
                5000.0,
                5000.0,
                5000.0
            };

            // Act
            var results = new List<double>();
            var defaultScriptObserverBackoffStrategy = new DefaultScriptObserverBackoffStrategy();

            for (var i = 0; i < testIteration; i++)
            {
                var result = defaultScriptObserverBackoffStrategy.GetBackoff(i);
                results.Add(result.TotalMilliseconds);
            }

            // Assert
            results.Should().BeEquivalentTo(expectedBackoffsInMiliseconds);
        }
    }
}