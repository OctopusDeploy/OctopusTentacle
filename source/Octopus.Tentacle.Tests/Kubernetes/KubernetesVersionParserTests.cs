using System;
using FluentAssertions;
using k8s.Models;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class KubernetesVersionParserTests
    {
        [TestCase("0", "1", 0, 1, false)]
        [TestCase("1abc", "1", 1, 1, false)]
        [TestCase("0", "1abc", 0, 1, false)]
        [TestCase("1+", "0", 1, 0, false)]
        [TestCase("0", "1+", 0, 1, false)]
        [TestCase("abc", "1", 999, 999, true)]
        public void ParseClusterVersion_SanitizesAndReturnsClusterVersion(string major, string minor, int expectedMajor, int expectedMinor, bool shouldFail)
        {
            try
            {
                var versionInfo = new VersionInfo
                {
                    Major = major,
                    Minor = minor
                };
                var result = KubernetesVersionParser.ParseClusterVersion(versionInfo);
                result.Should().BeEquivalentTo(new ClusterVersion(expectedMajor, expectedMinor));
            }
            catch (Exception e)
            {
                if (shouldFail)
                {
                    e.Should().BeOfType<FormatException>();
                }
                else
                {
                    throw;
                }
            }
        }
    }
}