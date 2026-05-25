using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.TestAttributes;
using Octopus.Tentacle.Util;
using PlatformDetection = Octopus.Tentacle.Util.PlatformDetection;

namespace Octopus.Tentacle.Tests.Integration.Util
{
    [TestFixture]
    public class SilentProcessRunnerFixture : IntegrationTest
    {
        const int SIG_TERM = 143;
        const int SIG_KILL = 137;
        string command;
        string commandParam;

        [SetUp]
        public void SetUpLocal()
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                command = "cmd.exe";
                commandParam = "/c";
            }
            else
            {
                command = "bash";
                commandParam = "-c";
            }
        }

        [Test]
        public void ExitCode_ShouldBeReturned()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var arguments = $"{commandParam} \"exit 99\"";
                var workingDirectory = "";

                var exitCode = Execute(command,
                    arguments,
                    workingDirectory,
                    out var debugMessages,
                    out var infoMessages,
                    out var errorMessages,
                    cts.Token);

                exitCode.Should().Be(99, "our custom exit code should be reflected");

                // It seems the encoding can change during the lifetime of the OS so don't expect it wont change in tests.
                debugMessages.ToString().Should().ContainEquivalentOf($"Starting {command} in working directory '' using '");
                debugMessages.ToString().Should().ContainEquivalentOf($"' encoding running as '{TestEnvironmentHelper.CurrentUserName}'");

                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
                infoMessages.ToString().Should().BeEmpty("no messages should be written to stdout");
            }
        }

        [Test]
        public void DebugLogging_ShouldContainDiagnosticsInfo_ForDefault()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var arguments = $"{commandParam} \"echo hello\"";
                var workingDirectory = "";

                var exitCode = Execute(command,
                    arguments,
                    workingDirectory,
                    out var debugMessages,
                    out var infoMessages,
                    out var errorMessages,
                    cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                debugMessages.ToString()
                    .Should()
                    .ContainEquivalentOf(command, "the command should be logged")
                    .And.ContainEquivalentOf(TestEnvironmentHelper.CurrentUserName, "the current user details should be logged");
                infoMessages.ToString().Should().ContainEquivalentOf("hello");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
            }
        }

        [Test]
        [Retry(3)]
        public void CancellationToken_ShouldForceKillTheProcess()
        {
            // Terminate the process after a very short time so the test doesn't run forever
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                // Starting a new instance of cmd.exe will run indefinitely waiting for user input
                var arguments = "";
                var workingDirectory = "";

                var exitCode = Execute(command,
                    arguments,
                    workingDirectory,
                    out var debugMessages,
                    out var infoMessages,
                    out var errorMessages,
                    cts.Token);

                if (PlatformDetection.IsRunningOnWindows)
                {
                    exitCode.Should().BeLessOrEqualTo(0, "the process should have been terminated");
                    infoMessages.ToString().Should().ContainEquivalentOf("Microsoft Windows", "the default command-line header would be written to stdout");
                }
                else
                {
                    exitCode.Should().BeOneOf(SIG_KILL, SIG_TERM, 0);
                }

                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
            }
        }

        [Test]
        [WindowsTest]
        public async Task CancellationToken_WhenGrandchildHoldsRedirectedPipes_ShouldNotHang()
        {
            using var tempDir = new TemporaryDirectory();
            var grandchildPidFile = Path.Combine(tempDir.DirectoryPath, "grandchild.pid");

            // This test reproduces the cancel-time hang.
            // The issue was SilentProcessRunner will wait for stdout/stderr pipes to be closed.
            // The pipes can be inherited by grandchildren, and remain open even after the
            // child process has died.
            //
            // Normally Process.Kill(entireProcessTree:true) would kill the entire process tree.
            // However if the child process dies THEN we issue the kill command, we do NOT see any
            // process under the child and so the Kill() command completes. This leaves the grandchild
            // running, holding our pipes we are waiting on.
            //
            // The test stacks three processes: PowerShell (the child) launches cmd.exe (a
            // throwaway middle layer) which does `start /b ping` to background ping (the
            // grandchild) and then exits. cmd exiting before we cancel is what breaks the
            // PPID chain — without that, ping would still be a direct child of PowerShell
            // and Kill(true) would find it.
            //
            // Two non-obvious bits below, both load-bearing:
            //   * $psi.RedirectStandardInput = $true — we don't use stdin, but redirecting
            //     any stream is what flips bInheritHandles=true in .NET's Process.Start. That
            //     is what makes cmd (and by extension ping) inherit our pipe write-ends.
            //     Without this the grandchild doesn't hold our pipes and there is no bug to
            //     reproduce.
            //   * The WMI lookup — we need the grandchild's PID so the test can clean it up.
            var psScript = @"
$pidFile = 'PIDFILE_PLACEHOLDER'
$pingPath = Join-Path $env:WINDIR 'System32\PING.EXE'
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = Join-Path $env:WINDIR 'System32\cmd.exe'
# -n 60000 just makes ping long-running enough to outlive the test.
$psi.Arguments = '/c start /b """" ""' + $pingPath + '"" -n 60000 127.0.0.1'
$psi.UseShellExecute = $false
$psi.CreateNoWindow  = $true
# Redirecting any stream flips bInheritHandles=true in .NET's Process.Start,
# so non-redirected streams pass through via GetStdHandle to the child.
$psi.RedirectStandardInput = $true
$cmd = [System.Diagnostics.Process]::Start($psi)
$cmd.StandardInput.Close()
$cmdPid = $cmd.Id
# Wait for cmd to exit -- breaks the PPID chain and makes Kill(true) miss ping.
$cmd.WaitForExit()
# Poll until ping appears in WMI — there's a lag between cmd exiting and the
# orphaned ping becoming visible.
$deadline = (Get-Date).AddSeconds(10)
while ((Get-Date) -lt $deadline) {
    $p = Get-CimInstance Win32_Process -Filter ""ParentProcessId=$cmdPid AND Name='PING.EXE'"" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($p) { Set-Content -Path $pidFile -Value $p.ProcessId; break }
    Start-Sleep -Milliseconds 100
}
";
            psScript = psScript.Replace("PIDFILE_PLACEHOLDER", grandchildPidFile);
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(psScript));

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    var task = Task.Run(() => Execute(
                        "powershell.exe",
                        $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
                        "",
                        out _,
                        out _,
                        out _,
                        cts.Token));

                    // Wait for the grandchild to actually be spawned before cancelling
                    await WaitForGrandchildSpawnAsync(grandchildPidFile, TimeSpan.FromSeconds(60));

                    var sw = Stopwatch.StartNew();
                    cts.Cancel();

                    var completed = task.Wait(TimeSpan.FromSeconds(30));
                    sw.Stop();

                    completed.Should().BeTrue(
                        $"ExecuteCommand should return shortly after cancellation even when a grandchild " +
                        $"holds the redirected pipes. Without proactively closing the redirected streams " +
                        $"after Kill, Process.WaitForExit() blocks indefinitely. Elapsed since cancel: {sw.Elapsed.TotalSeconds:F1}s");
                }
            }
            finally
            {
                TryKillGrandchild(grandchildPidFile);
            }
        }

        [Test]
        public async Task CancellationToken_WhenUnixGrandchildHoldsRedirectedPipes_ShouldNotHang()
        {
            if (PlatformDetection.IsRunningOnWindows)
                Assert.Ignore("Unix-only repro (Mac/Linux). The Windows equivalent is covered by the [WindowsTest] above.");

            // This test reproduces the cancel-time hang.
            // The issue is SilentProcessRunner will wait for stdout/stderr pipes to be closed.
            // The pipes can be inherited by grandchildren, and remain open even after the
            // child process has died.
            //
            // Normally Process.Kill(entireProcessTree:true) would kill the entire process tree.
            // However if the child process dies THEN we issue the kill command, we do NOT see any
            // process under the child and so the Kill() command completes. This leaves the grandchild
            // running, holding our pipes we are waiting on.
            //
            // The test is simple we run "sh", the child process, and tell it to yeet
            // sleep, the grandchild, into the background. The grandchild now keeps
            // running holding on to our pipes we will wait for an EOF on.

            using var tempDir = new TemporaryDirectory();
            var grandchildPidFile = Path.Combine(tempDir.DirectoryPath, "grandchild.pid");
            var grandchild = $"sleep 600 & echo $! > '{grandchildPidFile}'; exit 0";

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    var task = Task.Run(() => Execute(
                        "/bin/sh",
                        $"-c \"{grandchild}\"", // We run this in background and `sh` does not wait for it.
                        "",
                        out _,
                        out _,
                        out _,
                        cts.Token));

                    await WaitForGrandchildSpawnAsync(grandchildPidFile, TimeSpan.FromSeconds(30));

                    var sw = Stopwatch.StartNew();
                    cts.Cancel();

                    // Cancel should be super quick, if it takes a long time, then we have an issue where we are waiting for the grandchild.
                    var completed = task.Wait(TimeSpan.FromSeconds(30));
                    sw.Stop();

                    completed.Should().BeTrue(
                        $"ExecuteCommand should return shortly after cancellation even when a Unix " +
                        $"grandchild (reparented to init/launchd) holds the redirected pipes. " +
                        $"Without proactively closing the redirected streams after Kill, " +
                        $"Process.WaitForExit() blocks indefinitely. Elapsed since cancel: {sw.Elapsed.TotalSeconds:F1}s");
                }
            }
            finally
            {
                TryKillGrandchild(grandchildPidFile);
            }
        }

        static async Task WaitForGrandchildSpawnAsync(string pidFile, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(pidFile) && int.TryParse(SafelyReadAllText(pidFile).Trim(), out var pid) && pid > 0)
                    return;
                await Task.Delay(50);
            }
            throw new TimeoutException(
                $"Test setup failed: the grandchild PID was never written to '{pidFile}'. " +
                $"The grandchild-pipe scenario is not being exercised.");
        }

        static string SafelyReadAllText(string path)
        {
            try { return File.ReadAllText(path); } catch { return string.Empty; }
        }

        static void TryKillGrandchild(string pidFile)
        {
            try
            {
                if (File.Exists(pidFile) && int.TryParse(SafelyReadAllText(pidFile).Trim(), out var pid))
                {
                    try { Process.GetProcessById(pid).Kill(); } catch { /* already gone */ }
                }
            }
            catch { /* ignore */ }
        }

        [Test]
        public void EchoHello_ShouldWriteToStdOut()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var arguments = $"{commandParam} \"echo hello\"";
                var workingDirectory = "";

                var exitCode = Execute(command,
                    arguments,
                    workingDirectory,
                    out var debugMessages,
                    out var infoMessages,
                    out var errorMessages,
                    cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
                infoMessages.ToString().Should().ContainEquivalentOf("hello");
            }
        }

        [Test]
        public void EchoError_ShouldWriteToStdErr()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var arguments = $"{commandParam} \"echo Something went wrong! 1>&2\"";
                var workingDirectory = "";

                var exitCode = Execute(command,
                    arguments,
                    workingDirectory,
                    out var debugMessages,
                    out var infoMessages,
                    out var errorMessages,
                    cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                infoMessages.ToString().Should().BeEmpty("no messages should be written to stdout");
                errorMessages.ToString().Should().ContainEquivalentOf("Something went wrong!");
            }
        }

        [Test]
        public void RunAsCurrentUser_ShouldWork()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var arguments = PlatformDetection.IsRunningOnWindows
                    ? $"{commandParam} \"echo {EchoEnvironmentVariable("username")}\""
                    : $"{commandParam} \"whoami\"";
                var workingDirectory = "";

                var exitCode = Execute(command,
                    arguments,
                    workingDirectory,
                    out var debugMessages,
                    out var infoMessages,
                    out var errorMessages,
                    cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");
                infoMessages.ToString().Should().ContainEquivalentOf($@"{TestEnvironmentHelper.EnvironmentUserName}");
            }
        }

        [Test]
        [WindowsTest]
        [TestCase("powershell.exe", "-command \"Write-Host $env:userdomain\\$env:username\"")]
        public void RunAsCurrentUser_PowerShell_ShouldWork(string command, string arguments)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var workingDirectory = "";

                var exitCode = Execute(command,
                    arguments,
                    workingDirectory,
                    out var debugMessages,
                    out var infoMessages,
                    out var errorMessages,
                    cts.Token);

                exitCode.Should().Be(0, "the process should have run to completion");
                errorMessages.ToString().Should().BeEmpty("no messages should be written to stderr");

                infoMessages.ToString().Should().ContainEquivalentOf($@"{TestEnvironmentHelper.EnvironmentDomain}\{TestEnvironmentHelper.EnvironmentUserName}");
            }
        }

        static string EchoEnvironmentVariable(string varName)
        {
            if (PlatformDetection.IsRunningOnWindows)
                return $"%{varName}%";

            return $"${varName}";
        }

        static int Execute(
            string command,
            string arguments,
            string workingDirectory,
            out StringBuilder debugMessages,
            out StringBuilder infoMessages,
            out StringBuilder errorMessages,
            CancellationToken cancel)
        {
            var debug = new StringBuilder();
            var info = new StringBuilder();
            var error = new StringBuilder();

            // We're in a synchronous test helper (Execute) that exposes a sync int
            // return and out parameters. The method must return synchronously, so we
            // block on the async call with .GetAwaiter().GetResult(). This is
            // sync-over-async but is safe because the NUnit test runner dispatches us
            // on a worker thread without a captured SynchronizationContext, so no
            // deadlock.
            // See https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html
            var exitCode = SilentProcessRunner.ExecuteCommandAsync(
                command,
                arguments,
                workingDirectory,
                x =>
                {
                    Console.WriteLine($"{DateTime.UtcNow} DBG: {x}");
                    debug.Append(x);
                },
                x =>
                {
                    Console.WriteLine($"{DateTime.UtcNow} INF: {x}");
                    info.Append(x);
                },
                x =>
                {
                    Console.WriteLine($"{DateTime.UtcNow} ERR: {x}");
                    error.Append(x);
                },
                cancel: cancel).GetAwaiter().GetResult();

            debugMessages = debug;
            infoMessages = info;
            errorMessages = error;

            return exitCode;
        }
    }
}
