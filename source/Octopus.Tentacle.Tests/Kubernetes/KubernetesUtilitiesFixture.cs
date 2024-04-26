using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    public class KubernetesUtilitiesFixture
    {
        const ulong Megabyte = 1000 * 1000;
        const ulong Mebibyte = 1024 * 1024;
        const ulong Gibibyte = 1024 * 1024 * 1024;

        [TestCase("1Gi", 1 * Gibibyte)]
        [TestCase("10Gi", 10 * Gibibyte)]
        [TestCase("10Mi", 10 * Mebibyte)]
        [TestCase("1M", 1 * Megabyte)]
        public void CorrectlyParsesTotalSize(string sizeString, ulong expected)
        {
            KubernetesUtilities.GetResourceBytes(sizeString).Should().Be(expected);
        }
    }
}