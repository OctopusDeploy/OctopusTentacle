using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.Tests.Builders;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ScriptOrchestratorFactoryFixture
    {
        readonly CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(42));

        CancellationToken CancellationToken => cancellationTokenSource.Token;

        [Test]
        public async Task CreateOrchestratorReturnsV3AlphaByDefault()
        {
            // Arrange
            var factory = new ScriptOrchestratorFactoryBuilder()
                .Build();

            // Act
            var orchestrator = await factory.CreateOrchestrator(CancellationToken);

            // Assert
            orchestrator.Should().NotBeNull();
            orchestrator.Should().BeOfType<ScriptServiceV3AlphaOrchestrator>();
        }

        [Test]
        public async Task CreateOrchestratorReturnsV2WhenV3IsNotAvailable()
        {
            // Arrange
            var factory = new ScriptOrchestratorFactoryBuilder()
                .WithClientCapabilitiesServiceV2(builder => builder.WithCapability(nameof(IScriptServiceV2)))
                .Build();

            // Act
            var orchestrator = await factory.CreateOrchestrator(CancellationToken);

            // Assert
            orchestrator.Should().NotBeNull();
            orchestrator.Should().BeOfType<ScriptServiceV2Orchestrator>();
        }

        [Test]
        public async Task CreateOrchestratorReturnsV2WhenV3IsDisabledInTentacleOptions()
        {
            // Arrange
            var factory = new ScriptOrchestratorFactoryBuilder()
                .WithClientOptions(builder => builder.WithDisableScriptServiceV3Alpha(true))
                .Build();

            // Act
            var orchestrator = await factory.CreateOrchestrator(CancellationToken);

            // Assert
            orchestrator.Should().NotBeNull();
            orchestrator.Should().BeOfType<ScriptServiceV2Orchestrator>();
        }

        [Test]
        public async Task CreateOrchestratorReturnsV1WhenNeitherV2NorV3IsAvailable()
        {
            // Arrange
            var factory = new ScriptOrchestratorFactoryBuilder()
                .WithClientCapabilitiesServiceV2(builder => builder.ClearCapabilities())
                .Build();

            // Act
            var orchestrator = await factory.CreateOrchestrator(CancellationToken);

            // Assert
            orchestrator.Should().NotBeNull();
            orchestrator.Should().BeOfType<ScriptServiceV1Orchestrator>();
        }

        [Test]
        public async Task CreateOrchestratorReturnsV2WhenV2IsMaxVersion()
        {
            // Arrange
            var factory = new ScriptOrchestratorFactoryBuilder()
                .WithMaxScriptServiceVersion(ScriptServiceVersion.Version2)
                .Build();

            // Act
            var orchestrator = await factory.CreateOrchestrator(CancellationToken);

            // Assert
            orchestrator.Should().NotBeNull();
            orchestrator.Should().BeOfType<ScriptServiceV2Orchestrator>();
        }

        [Test]
        public async Task CreateOrchestratorReturnsV1WhenV1IsMaxVersion()
        {
            // Arrange
            var factory = new ScriptOrchestratorFactoryBuilder()
                .WithMaxScriptServiceVersion(ScriptServiceVersion.Version1)
                .Build();

            // Act
            var orchestrator = await factory.CreateOrchestrator(CancellationToken);

            // Assert
            orchestrator.Should().NotBeNull();
            orchestrator.Should().BeOfType<ScriptServiceV1Orchestrator>();
        }
    }
}