using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using NSubstitute;
using NSubstitute.Extensions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Core.Diagnostics;
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
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
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
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
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
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            sut.GetPathUsedBytes("/octopus").Should().Be(usedSize);
        }
        
        [Test]
        public void IfDuFailsWeLogCorrectly()
        {
            const ulong usedSize = 500 * Megabyte;
            var systemLog = new InMemoryLog();
            var spr = Substitute.For<ISilentProcessRunner>();

            spr.When(x => x.ExecuteCommand("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>()))
                .Do(x =>
                {
                    // stdout
                    x.ArgAt<Action<string>>(3).Invoke("500\t/octopus");
                    x.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                    
                    // stderr
                    x.ArgAt<Action<string>>(4).Invoke("no permission for foo");
                    x.ArgAt<Action<string>>(4).Invoke("also no permission for bar");
                });
            spr.ReturnsForAll(1);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(systemLog, spr, memoryCache);
            sut.GetPathUsedBytes("/octopus").Should().Be(usedSize);
            
            systemLog.GetLogsForCategory(LogCategory.Warning).Should().Contain("Could not reliably get disk space using du. Getting best approximation...");
            systemLog.GetLogsForCategory(LogCategory.Info).Should().Contain($"Du stdout returned 500\t/octopus, {usedSize}\t/octopus");
            systemLog.GetLogsForCategory(LogCategory.Info).Should().Contain("Du stderr returned no permission for foo, also no permission for bar");
        }

        [Test]
        public void IfDuFailsCompletelyReturnNull()
        {
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ReturnsForAll(1);
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            sut.GetPathUsedBytes("/octopus").Should().Be(null);
        }
        
        [Test]
        public void ReturnedValueShouldBeCached()
        {
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ReturnsForAll(1);
            var baseTime = DateTimeOffset.UtcNow;
            var clock = new TestClock(baseTime);
            var memoryCache = new MemoryCache(new MemoryCacheOptions(){ Clock = clock});
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            sut.GetPathUsedBytes("/octopus").Should().Be(null);
            
            const ulong usedSize = 500 * Megabyte;
            spr.When(x => x.ExecuteCommand("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>()))
                .Do(x =>
                {
                    x.ArgAt<Action<string>>(3).Invoke($"123\t/octopus");
                    x.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                });
            clock.UtcNow = baseTime + TimeSpan.FromSeconds(29);

            sut.GetPathUsedBytes("/octopus").Should().Be(null);
        }
        
        [Test]
        public void DuCacheExpiresAfter30Seconds()
        {
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ReturnsForAll(1);
            var baseTime = DateTimeOffset.UtcNow;
            var clock = new TestClock(baseTime);
            var memoryCache = new MemoryCache(new MemoryCacheOptions(){ Clock = clock});
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            sut.GetPathUsedBytes("/octopus").Should().Be(null);
            
            const ulong usedSize = 500 * Megabyte;
            spr.When(x => x.ExecuteCommand("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>()))
                .Do(x =>
                {
                    x.ArgAt<Action<string>>(3).Invoke($"123\t/octopus");
                    x.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                });

            clock.UtcNow = baseTime + TimeSpan.FromSeconds(30);
            
            sut.GetPathUsedBytes("/octopus").Should().Be(usedSize);
        }

    }

    public class TestClock : ISystemClock
    {
        public TestClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; set; }
    }
}