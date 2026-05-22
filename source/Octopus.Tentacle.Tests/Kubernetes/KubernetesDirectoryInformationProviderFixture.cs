using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        public async Task DuOutputParses()
        {
            const ulong usedSize = 500 * Megabyte;
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ExecuteCommandAsync("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>())
                .Returns(callInfo =>
                {
                    callInfo.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                    return Task.FromResult(0);
                });
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            (await sut.GetPathUsedBytesAsync("/octopus")).Should().Be(usedSize);
        }

        [Test]
        public async Task DuOutputParsesWithMultipleLines()
        {
            const ulong usedSize = 500 * Megabyte;
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ExecuteCommandAsync("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>())
                .Returns(callInfo =>
                {
                    callInfo.ArgAt<Action<string>>(3).Invoke($"500\t/octopus/extradir");
                    callInfo.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                    callInfo.ArgAt<Action<string>>(3).Invoke($"{usedSize+1000}\tTotal");
                    return Task.FromResult(0);
                });
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            (await sut.GetPathUsedBytesAsync("/octopus")).Should().Be(usedSize);
        }

        [Test]
        public async Task IfDuFailsWeStillGetData()
        {
            const ulong usedSize = 500 * Megabyte;
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ExecuteCommandAsync("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>())
                .Returns(callInfo =>
                {
                    callInfo.ArgAt<Action<string>>(3).Invoke($"500\t/octopus");
                    callInfo.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                    return Task.FromResult(1);
                });
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            (await sut.GetPathUsedBytesAsync("/octopus")).Should().Be(usedSize);
        }

        [Test]
        public async Task IfDuFailsWeLogCorrectly()
        {
            const ulong usedSize = 500 * Megabyte;
            var systemLog = new InMemoryLog();
            var spr = Substitute.For<ISilentProcessRunner>();

            spr.ExecuteCommandAsync("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>())
                .Returns(callInfo =>
                {
                    // stdout
                    callInfo.ArgAt<Action<string>>(3).Invoke("500\t/octopus");
                    callInfo.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");

                    // stderr
                    callInfo.ArgAt<Action<string>>(4).Invoke("no permission for foo");
                    callInfo.ArgAt<Action<string>>(4).Invoke("also no permission for bar");
                    return Task.FromResult(1);
                });
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(systemLog, spr, memoryCache);
            (await sut.GetPathUsedBytesAsync("/octopus")).Should().Be(usedSize);

            systemLog.GetLogsForCategory(LogCategory.Warning).Should().Contain("Could not reliably get disk space using du. Getting best approximation...");
            systemLog.GetLogsForCategory(LogCategory.Info).Should().Contain($"Du stdout returned 500\t/octopus, {usedSize}\t/octopus");
            systemLog.GetLogsForCategory(LogCategory.Info).Should().Contain("Du stderr returned no permission for foo, also no permission for bar");
        }

        [Test]
        public async Task IfDuFailsCompletelyReturnNull()
        {
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string>>(), Arg.Any<Action<string>>())
                .Returns(Task.FromResult(1));
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            (await sut.GetPathUsedBytesAsync("/octopus")).Should().Be(null);
        }

        [Test]
        public async Task ReturnedValueShouldBeCached()
        {
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string>>(), Arg.Any<Action<string>>())
                .Returns(Task.FromResult(1));
            var baseTime = DateTimeOffset.UtcNow;
            var clock = new TestClock(baseTime);
            var memoryCache = new MemoryCache(new MemoryCacheOptions(){ Clock = clock});
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            (await sut.GetPathUsedBytesAsync("/octopus")).Should().Be(null);

            const ulong usedSize = 500 * Megabyte;
            spr.ExecuteCommandAsync("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>())
                .Returns(callInfo =>
                {
                    callInfo.ArgAt<Action<string>>(3).Invoke($"123\t/octopus");
                    callInfo.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                    return Task.FromResult(0);
                });
            clock.UtcNow = baseTime + TimeSpan.FromSeconds(29);

            (await sut.GetPathUsedBytesAsync("/octopus")).Should().Be(null);
        }

        [Test]
        public async Task DuCacheExpiresAfter30Seconds()
        {
            var spr = Substitute.For<ISilentProcessRunner>();
            spr.ExecuteCommandAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string>>(), Arg.Any<Action<string>>())
                .Returns(Task.FromResult(1));
            var baseTime = DateTimeOffset.UtcNow;
            var clock = new TestClock(baseTime);
            var memoryCache = new MemoryCache(new MemoryCacheOptions(){ Clock = clock});
            var sut = new KubernetesDirectoryInformationProvider(Substitute.For<ISystemLog>(), spr, memoryCache);
            (await sut.GetPathUsedBytesAsync("/octopus")).Should().Be(null);

            const ulong usedSize = 500 * Megabyte;
            spr.ExecuteCommandAsync("du", "-s -B 1 /octopus", "/", Arg.Any<Action<string>>(), Arg.Any<Action<string>>())
                .Returns(callInfo =>
                {
                    callInfo.ArgAt<Action<string>>(3).Invoke($"123\t/octopus");
                    callInfo.ArgAt<Action<string>>(3).Invoke($"{usedSize}\t/octopus");
                    return Task.FromResult(0);
                });

            clock.UtcNow = baseTime + TimeSpan.FromSeconds(30);

            (await sut.GetPathUsedBytesAsync("/octopus")).Should().Be(usedSize);
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