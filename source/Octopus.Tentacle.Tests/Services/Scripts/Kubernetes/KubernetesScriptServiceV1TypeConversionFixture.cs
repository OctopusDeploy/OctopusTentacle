using System;
using System.Collections.Generic;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;
using Octopus.Tentacle.Services.Scripts.Kubernetes;

namespace Octopus.Tentacle.Tests.Services.Scripts.Kubernetes
{
    [TestFixture]
    public class KubernetesScriptServiceV1TypeConversionFixture
    {
        [Test]
        public void StartKubernetesScriptCommandV1AlphaIsConvertedToStartKubernetesScriptCommandV1()
        {
            // arrange
            var v1AlphaCommand = new StartKubernetesScriptCommandV1Alpha(
                new ScriptTicket(Guid.NewGuid().ToString()),
                "task-id",
                "script body",
                new[] { "arg 1", "arg 2" },
                ScriptIsolationLevel.FullIsolation,
                TimeSpan.FromDays(1),
                "mutex name",
                new PodImageConfiguration("image",
                    "url",
                    "username",
                    "password"),
                "service-account-name",
                new Dictionary<ScriptType, string>
                {
                    [ScriptType.Bash] = "bash script"
                },
                new[]
                {
                    new ScriptFile("my file", new DataStream())
                });
            
            //act
            var v1Command = v1AlphaCommand.ToV1();

            //assert
            v1Command.Should().BeEquivalentTo(v1AlphaCommand);
        }
        
        [Test]
        public void StartKubernetesScriptCommandV1AlphaIsConvertedToStartKubernetesScriptCommandV1_WithNoImageConfiguration()
        {
            // arrange
            var v1AlphaCommand = new StartKubernetesScriptCommandV1Alpha(
                new ScriptTicket(Guid.NewGuid().ToString()),
                "task-id",
                "script body",
                new[] { "arg 1", "arg 2" },
                ScriptIsolationLevel.FullIsolation,
                TimeSpan.FromDays(1),
                "mutex name",
                null,
                "service-account-name",
                new Dictionary<ScriptType, string>
                {
                    [ScriptType.Bash] = "bash script"
                },
                new[]
                {
                    new ScriptFile("my file", new DataStream())
                });
            
            //act
            var v1Command = v1AlphaCommand.ToV1();

            //assert
            v1Command.Should().BeEquivalentTo(v1AlphaCommand);
        }
        
        [Test]
        public void StartKubernetesScriptCommandV1AlphaIsConvertedToStartKubernetesScriptCommandV1_WithEmptyImageConfiguration()
        {
            // arrange
            var v1AlphaCommand = new StartKubernetesScriptCommandV1Alpha(
                new ScriptTicket(Guid.NewGuid().ToString()),
                "task-id",
                "script body",
                new[] { "arg 1", "arg 2" },
                ScriptIsolationLevel.FullIsolation,
                TimeSpan.FromDays(1),
                "mutex name",
                new PodImageConfiguration(),
                "service-account-name",
                new Dictionary<ScriptType, string>
                {
                    [ScriptType.Bash] = "bash script"
                },
                new[]
                {
                    new ScriptFile("my file", new DataStream())
                });
            
            //act
            var v1Command = v1AlphaCommand.ToV1();

            //assert
            v1Command.Should().BeEquivalentTo(v1AlphaCommand);
        }
        
        [Test]
        public void StartKubernetesScriptCommandV1AlphaIsConvertedToStartKubernetesScriptCommandV1_WithNoMutexName()
        {
            // arrange
            var v1AlphaCommand = new StartKubernetesScriptCommandV1Alpha(
                new ScriptTicket(Guid.NewGuid().ToString()),
                "task-id",
                "script body",
                new[] { "arg 1", "arg 2" },
                ScriptIsolationLevel.FullIsolation,
                TimeSpan.FromDays(1),
                null!,
                new PodImageConfiguration(),
                "service-account-name",
                new Dictionary<ScriptType, string>
                {
                    [ScriptType.Bash] = "bash script"
                },
                new[]
                {
                    new ScriptFile("my file", new DataStream())
                });
            
            //act
            var v1Command = v1AlphaCommand.ToV1();

            //assert
            v1Command.IsolationMutexName.Should().Be("RunningScript");
            v1Command.Should().BeEquivalentTo(v1AlphaCommand, config => config.Excluding(c => c.IsolationMutexName));
        }

        [Test]
        public void KubernetesScriptStatusResponseV1IsConvertedToKubernetesScriptStatusResponseV1Alpha()
        {
            // arrange
            var v1Command = new KubernetesScriptStatusResponseV1(
                new ScriptTicket(Guid.NewGuid().ToString()),
                ProcessState.Running,
                -1,
                new List<ProcessOutput>
                {
                    new(ProcessOutputSource.StdErr, "do a thing")
                },
                1000);

            // act
            var v1AlphaCommand = v1Command.ToV1Alpha();

            // assert
            v1AlphaCommand.Should().BeEquivalentTo(v1Command);
        }
    }
}