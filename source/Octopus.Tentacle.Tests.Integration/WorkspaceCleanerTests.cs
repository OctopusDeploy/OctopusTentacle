using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class WorkspaceCleanerTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenScriptServiceIsRunning_ThenWorkspaceIsNotDeleted(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var startScriptCommand = new StartScriptCommandV2Builder().WithScriptBody(b => b.Print("Hello")).Build();

            var existingHomeDirectory = new TemporaryDirectory();
            var existingWorkspaceDirectory = GivenExistingWorkspaceExists(existingHomeDirectory);
            
            var workspaceDirectory = string.Empty;
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(d =>
                        d.BeforeStartScript(async () =>
                            {
                                await Task.CompletedTask;
                                File.WriteAllText(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectory), "Existing log file");
                            }).BeforeCompleteScript(async (_, _) =>
                            {
                                //Ensure we have run as best we can (dummy workspace was deleted, and we delay a little in case it's still working)
                                await Wait.For(() => !Directory.Exists(existingWorkspaceDirectory), CancellationToken);
                                await Task.Delay(1000, CancellationToken);

                                Directory.Exists(workspaceDirectory).Should().BeTrue("Workspace should not have been cleaned up");
                            })
                            .Build())
                    .Build())
                .Build(CancellationToken);

            workspaceDirectory = GetWorkspaceDirectoryPath(existingHomeDirectory.DirectoryPath, startScriptCommand.ScriptTicket.TaskId);

            await clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, new InMemoryLog());

            Directory.Exists(workspaceDirectory).Should().BeFalse("Workspace should be naturally cleaned up after completion");
        }

        [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenTentacleClientCrashesAndTentacleNeverGotToldToComplete_ThenWorkspaceIsConsideredRunning_AndWillNotBeDeleted(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var startScriptCommand = new StartScriptCommandV2Builder().WithScriptBody(b => b.Print("Hello")).Build();

            var existingHomeDirectory = new TemporaryDirectory();
            var existingWorkspaceDirectory = GivenExistingWorkspaceExists(existingHomeDirectory);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(d =>
                        d.BeforeStartScript(async () =>
                            {
                                await Task.CompletedTask;
                                File.WriteAllText(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectory), "Existing log file");
                            })
                            .BeforeCompleteScript((_, _) => throw new NotImplementedException("Force failure to simulate tentacle client crashing, and ensure we do not complete the script"))
                        .Build())
                    .Build())
                .Build(CancellationToken);
            
            await AssertionExtensions
                .Should(() => clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, new InMemoryLog()))
                .ThrowAsync<NotImplementedException>();

            //Ensure we have run as best we can (dummy workspace was deleted, and we delay a little in case it's still working)
            await Wait.For(() => !Directory.Exists(existingWorkspaceDirectory), CancellationToken);
            await Task.Delay(1000, CancellationToken);

            var workspaceDirectory = GetWorkspaceDirectoryPath(clientAndTentacle.RunningTentacle.HomeDirectory, startScriptCommand.ScriptTicket.TaskId);

            Directory.Exists(workspaceDirectory).Should().BeTrue();
        }

        [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenTentacleStarts_WithWorkspaceOlderThanThreshold_ThenWorkspaceIsDeleted(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var existingHomeDirectory = new TemporaryDirectory();
            var existingWorkspaceDirectory = GivenExistingWorkspaceExists(existingHomeDirectory);
            File.WriteAllText(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectory), "Existing log file");

            await Task.Delay(1000, CancellationToken);

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .Build(CancellationToken);

            await Wait.For(() => !Directory.Exists(existingWorkspaceDirectory), CancellationToken);
        }

        [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenTentacleStarts_WithWorkspaceYoungerThanThreshold_ThenWorkspaceIsLeftAlone(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMinutes(30);

            var existingHomeDirectory = new TemporaryDirectory();
            var existingWorkspaceDirectory = GivenExistingWorkspaceExists(existingHomeDirectory);
            File.WriteAllText(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectory), "Existing log file");

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .Build(CancellationToken);

            await Task.Delay(1000, CancellationToken);

            Directory.Exists(existingWorkspaceDirectory).Should().BeTrue();
        }

        [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenTentacleIsRunning_WithWorkspaceThatBecomesOlderThanThreshold_ThenWorkspaceIsDeleted(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var existingHomeDirectory = new TemporaryDirectory();

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .Build(CancellationToken);

            await Task.Delay(1000, CancellationToken);

            var existingWorkspaceDirectory = GivenExistingWorkspaceExists(existingHomeDirectory);
            File.WriteAllText(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectory), "Existing log file");

            await Task.Delay(1000, CancellationToken);

            await Wait.For(() => !Directory.Exists(existingWorkspaceDirectory), CancellationToken);
        }
        
        static string GetWorkspaceDirectoryPath(string homeDirectory, string scriptTicket)
        {
            var workspaceDirectory = Path.Combine(
                homeDirectory,
                ScriptWorkspaceFactory.WorkDirectory,
                scriptTicket);
            return workspaceDirectory;
        }

        static string GivenExistingWorkspaceExists(TemporaryDirectory existingHomeDirectory)
        {
            var existingWorkspaceDirectory = GetWorkspaceDirectoryPath(existingHomeDirectory.DirectoryPath, "35024668-3FED-46B7-94E5-FA288292ABF9");
            Directory.CreateDirectory(existingWorkspaceDirectory);
            return existingWorkspaceDirectory;
        }
    }
}
