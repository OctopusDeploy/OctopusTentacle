using System;
using System.Collections.Generic;
using FluentAssertions;
using k8s.Models;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class ClusterVersionTests
    {
        [TestCase("0", "1", 0, 1, false)]
        [TestCase("1abc", "1", 1, 1, false)]
        [TestCase("0", "1abc", 0, 1, false)]
        [TestCase("1+", "0", 1, 0, false)]
        [TestCase("0", "1+", 0, 1, false)]
        [TestCase("abc", "1", 999, 999, true)]
        public void FromVersionInfo_SanitizesAndReturnsNewClusterVersion(string major, string minor, int expectedMajor, int expectedMinor, bool shouldFail)
        {
            try
            {
                var versionInfo = new VersionInfo
                {
                    Major = major,
                    Minor = minor
                };
                var result = ClusterVersion.FromVersionInfo(versionInfo);
                result.Should().BeEquivalentTo(new ClusterVersion(expectedMajor, expectedMinor));
            }
            catch (Exception e)
            {
                if (shouldFail)
                    e.Should().BeOfType<FormatException>();
                else
                    throw;
            }
        }

        static IEnumerable<TestCaseData> FromVersionTestData()
        {
            yield return new TestCaseData(new Version("0.0.1"), 0, 0);
            yield return new TestCaseData(new Version("1.31"), 1, 31);
            yield return new TestCaseData(new Version("2.24.4"), 2, 24);
        }

        [TestCaseSource(nameof(FromVersionTestData))]
        public void FromVersion_ReturnsNewClusterVersion(Version version, int expectedMajor, int expectedMinor)
        {
            var result = ClusterVersion.FromVersion(version);
            result.Should().BeEquivalentTo(new ClusterVersion(expectedMajor, expectedMinor));
        }

        static IEnumerable<TestCaseData> CompareClusterVersionsTestData()
        {
            yield return new TestCaseData(new ClusterVersion(0, 0), null, 1);
            yield return new TestCaseData(new ClusterVersion(0, 0), new ClusterVersion(0, 0), 0);
            yield return new TestCaseData(new ClusterVersion(0, 5), new ClusterVersion(0, 6), -1);
            yield return new TestCaseData(new ClusterVersion(1, 1), new ClusterVersion(2, 0), -1);
            yield return new TestCaseData(new ClusterVersion(1, 30), new ClusterVersion(1, 29), 1);
            yield return new TestCaseData(new ClusterVersion(3, 0), new ClusterVersion(2, 11), 1);
            yield return new TestCaseData(new ClusterVersion(3, 14), new ClusterVersion(3, 14), 0);
        }

        [TestCaseSource(nameof(CompareClusterVersionsTestData))]
        public void CompareClusterVersions(ClusterVersion thisClusterVersion, ClusterVersion otherClusterVersion, int expected)
        {
            var result = thisClusterVersion.CompareTo(otherClusterVersion);
            result.Should().Be(expected);
        }

        static IEnumerable<TestCaseData> CheckEqualityClusterVersionsTestData()
        {
            yield return new TestCaseData(new ClusterVersion(0, 0), null, false);
            yield return new TestCaseData(new ClusterVersion(0, 5), new ClusterVersion(0, 6), false);
            yield return new TestCaseData(new ClusterVersion(1, 0), new ClusterVersion(2, 0), false);
            yield return new TestCaseData(new ClusterVersion(3, 14), new ClusterVersion(3, 14), true);
        }

        [TestCaseSource(nameof(CheckEqualityClusterVersionsTestData))]
        public void CheckEqualityClusterVersions(ClusterVersion thisClusterVersion, ClusterVersion otherClusterVersion, bool expected)
        {
            var result = thisClusterVersion.Equals(otherClusterVersion);
            result.Should().Be(expected);
        }
    }
}