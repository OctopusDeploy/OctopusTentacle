using System;
using FluentAssertions;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    public class KubernetesDirectoryInformationProviderFixture
    {
        // Sizes
        const ulong Megabyte = 1000 * 1000;
        
        [Test]
        public void DuOutputParses()
        {
            const ulong usedSize = 500 * Megabyte;
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.When(x => x.ExecuteCommand("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>()))
                .Do(x =>
                {
                    x.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                });
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr);
            sut.GetPathUsedBytes("/octopus").Should().Be(usedSize);
        }
        
        [Test]
        public void DuOutputParsesWithMultipleLines()
        {
            const ulong usedSize = 500 * Megabyte;
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.When(x => x.ExecuteCommand("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>()))
                .Do(x =>
                {
                    x.ArgAt<Action<string>>(3).Invoke($"500\t/octopus/extradir");
                    x.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                    x.ArgAt<Action<string>>(3).Invoke($"{usedSize+1000}\tTotal");
                });
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr);
            sut.GetPathUsedBytes("/octopus").Should().Be(usedSize);
        }

        [Test]
        public void IfDuFailsWeStillGetData()
        {
            const ulong usedSize = 500 * Megabyte;
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.When(x => x.ExecuteCommand("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>()))
                .Do(x =>
                {
                    x.ArgAt<Action<string>>(3).Invoke($"500\t/octopus");
                    x.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                });
            spr.ReturnsForAll(1);
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr);
            sut.GetPathUsedBytes("/octopus").Should().Be(usedSize);
        }
        
        [Test]
        public void IfDuFailsCompletelyReturnNull()
        {
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ReturnsForAll(1);
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr);
            sut.GetPathUsedBytes("/octopus").Should().Be(null);
        }
    }
}