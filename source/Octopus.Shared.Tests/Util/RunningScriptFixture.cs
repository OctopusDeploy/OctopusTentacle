using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration;
using Octopus.Shared.Contracts;
using Octopus.Shared.Scripts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    // These tests are flakey on the build server.
    // Sometimes powershell just returns -1 when running these scripts.
    // That's why every test has a retry attribute.

    [TestFixture]
    public class RunningScriptFixture
    {
        readonly string testRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), $"OctopusTest-{nameof(RunningScriptFixture)}");
        TemporaryDirectory temporaryDirectory;
        CancellationTokenSource cancellationTokenSource;
        private string taskId;
        IScriptWorkspace workspace;
        TestScriptLog scriptLog;
        RunningScript runningScript;
        TestUserPrincipal user;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = new TemporaryDirectory(testRootPath);
            var homeConfiguration = Substitute.For<IHomeConfiguration>();
            homeConfiguration.HomeDirectory.Returns(temporaryDirectory.DirectoryPath);
            homeConfiguration.ApplicationSpecificHomeDirectory.Returns(temporaryDirectory.DirectoryPath);
            var workspaceFactory = new ScriptWorkspaceFactory(new OctopusPhysicalFileSystem(), homeConfiguration);
            taskId = Guid.NewGuid().ToString();
            workspace = workspaceFactory.GetWorkspace(new ScriptTicket(taskId));
            Console.WriteLine($"Working directory: {workspace.WorkingDirectory}");
            scriptLog = new TestScriptLog();
            cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            runningScript = new RunningScript(workspace, scriptLog, taskId, cancellationTokenSource.Token);
            user = new TestUserPrincipal("test-runningscript");
        }

        [TearDown]
        public void TearDown()
        {
            cancellationTokenSource.Dispose();
            temporaryDirectory.Dispose();
        }

        [Test]
        [Retry(3)]
        public void ExitCode_ShouldBeReturned()
        {
            workspace.BootstrapScript("exit 9999");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(9999, "the exit code of the script should be returned");

        }

        [Test]
        [Retry(3)]
        public void WriteHost_WritesToStdOut_AndIsReturned()
        {
            workspace.BootstrapScript("Write-Host Hello");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf("Hello", "the message should have been written to stdout");
        }

        [Test]
        [Retry(3)]
        public void WriteDebug_DoesNotWriteAnywhere()
        {
            workspace.BootstrapScript("Write-Debug Hello");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdOut.Length.Should().Be(0, "the script shouldn't have written to stdout");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
        }

        [Test]
        [Retry(3)]
        public void WriteOutput_WritesToStdOut_AndIsReturned()
        {
            workspace.BootstrapScript("Write-Output Hello");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf("Hello", "the message should have been written to stdout");
        }

        [Test]
        [Retry(3)]
        public void WriteError_WritesToStdErr_AndIsReturned()
        {
            workspace.BootstrapScript("Write-Error EpicFail");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(1, "Write-Error causes the exit code to be 1");
            scriptLog.StdOut.Length.Should().Be(0, "the script shouldn't have written to stdout");
            scriptLog.StdErr.ToString().Should().ContainEquivalentOf("EpicFail", "the message should have been written to stderr");
        }

        [Test]
        [Retry(3)]
        public void RunAsCurrentUser_ShouldWork()
        {
            workspace.BootstrapScript("Write-Host $env:userdomain\\$env:username");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf($@"{Environment.UserDomainName}\{Environment.UserName}");
        }

        [Test]
        [Retry(3)]
        public void RunAsDifferentUser_ShouldWork()
        {
            workspace.BootstrapScript("Write-Host $env:userdomain\\$env:username");
            workspace.RunAs = user.GetCredential();
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf($@"{user.DomainName}\{user.UserName}");
        }

        [Test]
        [Retry(3)]
        public void RunAsDifferentUser_ShouldWork_TempPath()
        {
            workspace.BootstrapScript("Write-Host Attempting to create a file in $env:temp\n\"hello\" | Out-File $env:temp\\hello.txt\ndir $env:temp");
            workspace.RunAs = user.GetCredential();
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf("hello.txt", "the dir command should have logged the presence of the file we just wrote");
        }

        [Test]
        [Retry(5)]
        public void CancellationToken_ShouldKillTheProcess()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var script = new RunningScript(workspace, scriptLog, taskId, cts.Token);
                workspace.BootstrapScript("Write-Host Starting\nStart-Sleep -seconds 10\nWrite-Host Finito");
                script.Execute();
                runningScript.ExitCode.Should().Be(0, "the script should have been canceled");
                scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
                scriptLog.StdOut.ToString().Should().ContainEquivalentOf("Starting", "the starting message should be written to stdout");
                scriptLog.StdOut.ToString().Should().NotContainEquivalentOf("Finito", "the script should have canceled before writing the finish message");
            }
        }

        [Test]
        [Retry(3)]
        public void RunAsCurrentUser_ShouldCopyCustomEnvironmentVariables()
        {
            workspace.CustomEnvironmentVariables.Add("customenvironmentvariable", "customvalue");
            workspace.BootstrapScript("Write-Host $env:customenvironmentvariable");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf("customvalue");
        }

        [Test]
        [Retry(3)]
        public void RunAsDifferentUser_ShouldCopyCustomEnvironmentVariables()
        {
            workspace.RunAs = user.GetCredential();
            workspace.CustomEnvironmentVariables.Add("customenvironmentvariable", "customvalue");
            workspace.BootstrapScript("Write-Host $env:customenvironmentvariable");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf("customvalue");
        }
    }
}