using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
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
        public async Task WhenScriptServiceIsRunningAndWritesLogFile_ThenWorkspaceIsNotDeleted(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var existingHomeDirectory = new TemporaryDirectory();

            var waitBeforeCompletingScriptFile = Path.Combine(existingHomeDirectory.DirectoryPath, "WaitForMeToExist.txt");
            var startScriptCommand = new TestExecuteShellScriptCommandBuilder().SetScriptBody(b => b.WaitForFileToExist(waitBeforeCompletingScriptFile)).Build();
            var startScriptWorkspaceDirectory = GetWorkspaceDirectoryPath(existingHomeDirectory.DirectoryPath, startScriptCommand.ScriptTicket.TaskId);

            var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .Build(CancellationToken);

            // Start task
            var runningScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, new InMemoryLog());
            await Wait.For(() => Directory.Exists(startScriptWorkspaceDirectory), 
                TimeSpan.FromSeconds(60),
                () => throw new Exception("Workspace directory did not get created"),
                CancellationToken);

            // Ensure Workspace Cleaning Has Run
            var existingWorkspaceDirectory = GivenExistingWorkspaceExists(existingHomeDirectory);
            await File.WriteAllTextAsync(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectory), "Existing log file");
            await Wait.For(() => !Directory.Exists(existingWorkspaceDirectory), 
                TimeSpan.FromSeconds(60),
                () => throw new Exception("Workspace directory did not get deleted"),
                CancellationToken);

            Directory.Exists(startScriptWorkspaceDirectory).Should().BeTrue("Workspace should not have been cleaned up");

            await File.WriteAllTextAsync(waitBeforeCompletingScriptFile, "Write file that makes script continue executing");
            await runningScriptTask;
        }
        
                [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenScriptServiceIsRunningAndWritesBootstrapScript_ThenWorkspaceIsNotDeleted(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var existingHomeDirectory = new TemporaryDirectory();

            var waitBeforeCompletingScriptFile = Path.Combine(existingHomeDirectory.DirectoryPath, "WaitForMeToExist.txt");
            var startScriptCommand = new TestExecuteShellScriptCommandBuilder().SetScriptBody(b => b.WaitForFileToExist(waitBeforeCompletingScriptFile)).Build();
            var startScriptWorkspaceDirectory = GetWorkspaceDirectoryPath(existingHomeDirectory.DirectoryPath, startScriptCommand.ScriptTicket.TaskId);

            var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .Build(CancellationToken);

            // Start task
            var runningScriptTask = clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, new InMemoryLog());
            await Wait.For(() => Directory.Exists(startScriptWorkspaceDirectory), 
                TimeSpan.FromSeconds(60),
                () => throw new Exception("Workspace directory did not get created"),
                CancellationToken);

            // Ensure Workspace Cleaning Has Run
            var existingWorkspaceDirectory = GivenExistingWorkspaceExists(existingHomeDirectory);
            await File.WriteAllTextAsync(GetBootstrapScriptFilePath(existingWorkspaceDirectory), "Existing bootstrap file");
            await Wait.For(() => !Directory.Exists(existingWorkspaceDirectory), 
                TimeSpan.FromSeconds(60),
                () => throw new Exception("Workspace directory did not get deleted"),
                CancellationToken);

            Directory.Exists(startScriptWorkspaceDirectory).Should().BeTrue("Workspace should not have been cleaned up");

            await File.WriteAllTextAsync(waitBeforeCompletingScriptFile, "Write file that makes script continue executing");
            await runningScriptTask;
        }

        [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenCompleteScriptIsNotCalled_ThenWorkspaceShouldGetDeletedWhenScriptFinishesRunning(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder().SetScriptBody(b => b.Print("Hello")).Build();

            var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .DecorateAllScriptServicesWith(u => u
                        .BeforeCompleteScript(
                            () => throw new NotImplementedException("Force failure to simulate tentacle client crashing, and ensure we do not complete the script")))
                    .Build())
                .Build(CancellationToken);

            await AssertionExtensions
                .Should(() => clientAndTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, null, new InMemoryLog()))
                .ThrowAsync<NotImplementedException>();

            var workspaceDirectory = GetWorkspaceDirectoryPath(clientAndTentacle.RunningTentacle.HomeDirectory, startScriptCommand.ScriptTicket.TaskId);
            
            await Wait.For(() => !Directory.Exists(workspaceDirectory), 
                TimeSpan.FromSeconds(20),
                () =>
                {
                    try
                    {
                        Directory.Delete(workspaceDirectory, true);
                    }
                    catch (Exception)
                    {
                        // Deleting a worksapce is best effort and can silently fail if it is in use / locked by something.
                        // If the cleaner failed to delete the directory and we cannot delete it in the test we can assume that it 
                        // is a valid failure and the test was successful.

                        return;
                    }

                    throw new Exception("Workspace directory did not get deleted by the workspace cleaner but the test was able to delete it. This indicates there is an issue in the Tentacle code.");
                },
                CancellationToken);
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
            await File.WriteAllTextAsync(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectoryWithLogFile), "Existing log file");

            var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .Build(CancellationToken);

            await Wait.For(() => !Directory.Exists(existingWorkspaceDirectoryWithLogFile), 
                TimeSpan.FromSeconds(60),
                () => throw new Exception("Workspace directory did not get deleted"),
                CancellationToken);
            Directory.Exists(existingWorkspaceDirectoryWithoutLogFile).Should().BeTrue();
        }
        
        [Test]
        [TentacleConfigurations(testDefaultTentacleRuntimeOnly: true)]
        public async Task WhenTentacleStarts_WithWorkspacesOlderThanThreshold_ThenWorkspaceWithBootstrapFileIsDeleted(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var cleanerDelay = TimeSpan.FromMilliseconds(500);
            var deleteWorkspacesOlderThan = TimeSpan.FromMilliseconds(500);

            var existingHomeDirectory = new TemporaryDirectory();

            var existingWorkspaceDirectoryWithoutLogFile = GivenExistingWorkspaceExists(existingHomeDirectory);
            var existingWorkspaceDirectoryWithLogFile = GivenExistingWorkspaceExists(existingHomeDirectory);
            await File.WriteAllTextAsync(GetBootstrapScriptFilePath(existingWorkspaceDirectoryWithLogFile), "Existing bootstrap file");

            var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(b =>
                {
                    b.WithHomeDirectory(existingHomeDirectory)
                        .WithWorkspaceCleaningSettings(cleanerDelay, deleteWorkspacesOlderThan);
                })
                .Build(CancellationToken);

            await Wait.For(() => !Directory.Exists(existingWorkspaceDirectoryWithLogFile), 
                TimeSpan.FromSeconds(60),
                () => throw new Exception("Workspace directory did not get deleted"),
                CancellationToken);
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
            await File.WriteAllTextAsync(ScriptWorkspace.GetLogFilePath(existingWorkspaceDirectory), "Existing log file");

            var clientAndTentacle = await tentacleConfigurationTestCase.CreateBuilder()
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

        static string GetBootstrapScriptFilePath(string workingDirectory)
        {
            return !PlatformDetection.IsRunningOnWindows
                ? BashScriptWorkspace.GetBashBootstrapScriptFilePath(workingDirectory)
                : ScriptWorkspace.GetBootstrapScriptFilePath(workingDirectory);
        }
    }
}