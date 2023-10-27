using System;
using System.IO;
using System.Threading;
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

            var existingHomeDirectory = new TemporaryDirectory();

            var waitBeforeCompletingScriptFile = Path.Combine(existingHomeDirectory.DirectoryPath, "WaitForMeToExist.txt");
            var startScriptCommand = new StartScriptCommandV3AlphaBuilder().WithScriptBody(b => b.WaitForFileToExist(waitBeforeCompletingScriptFile)).Build();
            var startScriptWorkspaceDirectory = GetWorkspaceDirectoryPath(existingHomeDirectory.DirectoryPath, startScriptCommand.ScriptTicket.TaskId);
            
            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .Build(CancellationToken);
            
            // Start task
            var runningScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, new InMemoryLog());
            await Wait.For(() => Directory.Exists(startScriptWorkspaceDirectory), CancellationToken);

            // Ensure Workspace Cleaning Has Run
            var existingWorkspaceDirectory = GivenExistingWorkspaceExists(existingHomeDirectory);
            File.WriteAllText(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectory), "Existing log file");
            await Wait.For(() => !Directory.Exists(existingWorkspaceDirectory), CancellationToken);

            Directory.Exists(startScriptWorkspaceDirectory).Should().BeTrue("Workspace should not have been cleaned up");

            File.WriteAllText(waitBeforeCompletingScriptFile, "Write file that makes script continue executing");
            await runningScriptTask;

            Directory.Exists(startScriptWorkspaceDirectory).Should().BeFalse("Workspace should be naturally cleaned up after completion");
        }

        [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenCompleteScriptIsNotCalled_ThenWorkspaceShouldGetDeletedWhenScriptFinishesRunning(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder().WithScriptBody(b => b.Print("Hello")).Build();

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(d =>
                        d.BeforeCompleteScript((_, _) => throw new NotImplementedException("Force failure to simulate tentacle client crashing, and ensure we do not complete the script"))
                        .Build())
                    .Build())
                .Build(CancellationToken);
            
            await AssertionExtensions
                .Should(() => clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, new InMemoryLog()))
                .ThrowAsync<NotImplementedException>();
            
            var workspaceDirectory = GetWorkspaceDirectoryPath(clientAndTentacle.RunningTentacle.HomeDirectory, startScriptCommand.ScriptTicket.TaskId);

            await Wait.For(() => !Directory.Exists(workspaceDirectory), CancellationToken);
        }

        [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenTentacleStarts_WithWorkspacesOlderThanThreshold_ThenWorkspaceWithLogFileIsDeleted(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var existingHomeDirectory = new TemporaryDirectory();

            var existingWorkspaceDirectoryWithoutLogFile = GivenExistingWorkspaceExists(existingHomeDirectory);
            var existingWorkspaceDirectoryWithLogFile = GivenExistingWorkspaceExists(existingHomeDirectory);
            File.WriteAllText(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectoryWithLogFile), "Existing log file");

            await using var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .Build(CancellationToken);

            await Wait.For(() => !Directory.Exists(existingWorkspaceDirectoryWithLogFile), CancellationToken);
            Directory.Exists(existingWorkspaceDirectoryWithoutLogFile).Should().BeTrue();
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
            var existingWorkspaceDirectory = GetWorkspaceDirectoryPath(existingHomeDirectory.DirectoryPath, Guid.NewGuid().ToString());
            Directory.CreateDirectory(existingWorkspaceDirectory);
            return existingWorkspaceDirectory;
        }
    }
}
