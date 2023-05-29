using System;
using System.IO;
using System.Threading;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    // These tests are flakey on the build server.
    // Sometimes powershell just returns -1 when running these scripts.
    // That's why every test has a retry attribute.
    [TestFixture]
    [NonParallelizable]
    public class RunningScriptFixture : IntegrationTest
    {
        TemporaryDirectory temporaryDirectory;
        CancellationTokenSource cancellationTokenSource;
        string taskId;
        IScriptWorkspace workspace;
        TestScriptLog scriptLog;
        RunningScript runningScript;
        TestUserPrincipal user;

        [SetUp]
        public void SetUpLocal()
        {
            string testRootPath;
            IShell shell;

            if (PlatformDetection.IsRunningOnWindows)
            {
                testRootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), $"OctopusTest-{nameof(RunningScriptFixture)}");
                shell = new PowerShell();
                user = new TestUserPrincipal("test-runningscript");
            }
            else
            {
                testRootPath = Path.Combine(Path.GetTempPath(), $"OctopusTest-{nameof(RunningScriptFixture)}");
                shell = new Bash();
            }

            temporaryDirectory = new TemporaryDirectory(Substitute.For<IOctopusFileSystem>(), testRootPath);
            var homeConfiguration = Substitute.For<IHomeConfiguration>();
            homeConfiguration.HomeDirectory.Returns(temporaryDirectory.DirectoryPath);
            homeConfiguration.ApplicationSpecificHomeDirectory.Returns(temporaryDirectory.DirectoryPath);
            var log = new InMemoryLog();
            var workspaceFactory = new ScriptWorkspaceFactory(new OctopusPhysicalFileSystem(log), homeConfiguration, new SensitiveValueMasker());
            taskId = Guid.NewGuid().ToString();
            workspace = workspaceFactory.GetWorkspace(new ScriptTicket(taskId));
            Console.WriteLine($"Working directory: {workspace.WorkingDirectory}");
            scriptLog = new TestScriptLog();
            cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            runningScript = new RunningScript(shell,
                workspace,
                scriptLog,
                taskId,
                cancellationTokenSource.Token,
                log);
        }

        [TearDown]
        public void TearDownLocal()
        {
            cancellationTokenSource.Dispose();
            temporaryDirectory.Dispose();
        }

        [Test]
        [Retry(3)]
        public void ExitCode_ShouldBeReturned()
        {
            workspace.BootstrapScript("exit 99");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(99, "the exit code of the script should be returned");
        }

        [Test]
        [Retry(3)]
        public void WriteHost_WritesToStdOut_AndIsReturned()
        {
            workspace.BootstrapScript("echo Hello");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf("Hello", "the message should have been written to stdout");
        }

        [Test]
        [Retry(3)]
        [WindowsTest]
        public void WriteDebug_DoesNotWriteAnywhere()
        {
            workspace.BootstrapScript("Write-Debug Hello");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdOut.ToString().Should().NotContain("Hello", "the script shouldn't have written to stdout");
            scriptLog.StdErr.ToString().Should().NotContain("Hello", "the script shouldn't have written to stderr");
        }

        [Test]
        [Retry(3)]
        [WindowsTest]
        public void WriteOutput_WritesToStdOut_AndIsReturned()
        {
            workspace.BootstrapScript("Write-Output Hello");
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.ToString().Should().NotContain("Hello", "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf("Hello", "the message should have been written to stdout");
        }

        [Test]
        [Retry(3)]
        public void WriteError_WritesToStdErr_AndIsReturned()
        {
            workspace.BootstrapScript(PlatformDetection.IsRunningOnWindows ? "Write-Error EpicFail" : "&2 echo EpicFail");

            runningScript.Execute();
            if (PlatformDetection.IsRunningOnWindows)
                runningScript.ExitCode.Should().Be(1, "Write-Error causes the exit code to be 1");
            else
                runningScript.ExitCode.Should().Be(2, "&2 echo causes the exit code to be 1");

            scriptLog.StdOut.ToString().Should().NotContain("EpicFail", "the script shouldn't have written to stdout");
            scriptLog.StdErr.ToString().Should().ContainEquivalentOf("EpicFail", "the message should have been written to stderr");
        }

        [Test]
        [Retry(3)]
        public void RunAsCurrentUser_ShouldWork()
        {
            var scriptBody = PlatformDetection.IsRunningOnWindows
                ? $"echo {EchoEnvironmentVariable("username")}"
                : "whoami";
            workspace.BootstrapScript(scriptBody);
            runningScript.Execute();
            runningScript.ExitCode.Should().Be(0, "the script should have run to completion");
            scriptLog.StdErr.Length.Should().Be(0, "the script shouldn't have written to stderr");
            scriptLog.StdOut.ToString().Should().ContainEquivalentOf($@"{Environment.UserName}");
        }

        [Test]
        [Retry(5)]
        public void CancellationToken_ShouldKillTheProcess()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var (shell, sleepCommand) = PlatformDetection.IsRunningOnWindows
                    ? (new PowerShell(), "Start-Sleep -seconds")
                    : (new Bash() as IShell, "sleep");

                var script = new RunningScript(shell,
                    workspace,
                    scriptLog,
                    taskId,
                    cts.Token,
                    new InMemoryLog());

                workspace.BootstrapScript($"echo Starting\n{sleepCommand} 30\necho Finito");
                script.Execute();
                runningScript.ExitCode.Should().Be(0, "the script should have been canceled");
                scriptLog.StdErr.ToString().Should().Be("", "the script shouldn't have written to stderr");
                scriptLog.StdOut.ToString().Should().ContainEquivalentOf("Starting", "the starting message should be written to stdout");
                scriptLog.StdOut.ToString().Should().NotContainEquivalentOf("Finito", "the script should have canceled before writing the finish message");
            }
        }

        static string EchoEnvironmentVariable(string varName)
        {
            if (PlatformDetection.IsRunningOnWindows)
                return $"$env:{varName}";

            return $"${varName}";
        }
    }
}