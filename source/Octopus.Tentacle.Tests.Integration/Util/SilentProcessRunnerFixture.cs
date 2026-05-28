using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Contracts;
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

            // This test guards the cancel path of ExecuteCommandAsync against a regression
            // involving re-parented grandchildren that inherit our redirected pipes.
            //
            // The scenario:
            //   * We launch a process that spawns a child, which spawns a long-running grandchild
            //     and then immediately exits.
            //   * Because the child exited BEFORE our cancel fires, the grandchild has been
            //     re-parented (PPID broken). Process.Kill(entireProcessTree:true) follows PPID
            //     links, so it does NOT find the grandchild — Kill returns having killed nothing
            //     beyond the (already-dead) child.
            //   * The grandchild inherited our redirected stdout/stderr pipes and holds them
            //     open. The stream readers therefore never see EOF.
            //
            // Why this is a real risk to ExecuteCommandAsync:
            //   * Old sync version: process.WaitForExit() blocks until BOTH the process exits
            //     AND the redirected streams reach EOF, so the grandchild holding pipes would
            //     hang it forever. The fix was to call process.Close() during cancel-cleanup to
            //     forcibly release the pipe handles.
            //   * New async version: WaitForExitAsync does NOT wait on streams — it returns as
            //     soon as the Exited event fires. SafelyWaitForAllOutput then waits up to 5s
            //     per stream for EOF and times out if the grandchild still holds them. Pipes
            //     are released by the using-block's Process.Dispose at end of method.
            //   * Critically, we deliberately do NOT call process.Close() during cancel-cleanup
            //     anymore — see DoOurBestToCleanUp in SilentProcessRunner.cs for the full
            //     explanation. Adding it back caused a 10-minute hang in CI because Close races
            //     with the Exited event handler that WaitForExitAsync depends on.
            //
            // This test asserts cancel returns in well under 30s in the grandchild scenario.
            // If it ever takes 10 minutes (the test timeout), someone has re-introduced
            // process.Close() or otherwise broken the Exited-event path.
            //
            // Two non-obvious bits in the PowerShell script below, both load-bearing:
            //   * $psi.RedirectStandardInput = $true — we don't use stdin, but redirecting
            //     any stream is what flips bInheritHandles=true in .NET's Process.Start. That
            //     is what makes cmd (and by extension ping) inherit our pipe write-ends.
            //     Without this the grandchild doesn't hold our pipes and there is no bug to
            //     reproduce.
            //   * The WMI lookup — we need the grandchild's PID so the test can clean it up
            //     afterwards (otherwise we'd leak a long-running ping on the CI host).
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
                    await WaitForPidFileAsync(grandchildPidFile, TimeSpan.FromSeconds(60));

                    var sw = Stopwatch.StartNew();
                    cts.Cancel();

                    var completed = task.Wait(TimeSpan.FromSeconds(30));
                    sw.Stop();

                    completed.Should().BeTrue(
                        $"ExecuteCommandAsync should return promptly after cancellation even when a " +
                        $"grandchild holds the redirected pipes. Worst case is ~10s (5s timeout × 2 streams " +
                        $"in SafelyWaitForAllOutput). If we hit the 30s test timeout, either someone " +
                        $"re-introduced process.Close() in DoOurBestToCleanUp (which races with the Exited " +
                        $"event WaitForExitAsync depends on) or SafelyWaitForAllOutput's per-stream timeout " +
                        $"has been removed. Elapsed since cancel: {sw.Elapsed.TotalSeconds:F1}s");
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

            // Unix equivalent of CancellationToken_WhenGrandchildHoldsRedirectedPipes_ShouldNotHang
            // above. See that test's leading comment for the full rationale — the short version is:
            //
            //   * We start sh, which backgrounds a long sleep (the grandchild) and exits
            //     immediately. The grandchild gets re-parented to init/launchd and inherits our
            //     redirected stdout/stderr pipes, holding them open.
            //   * Process.Kill(entireProcessTree:true) follows PPID links, so by the time we
            //     cancel, the now-orphan grandchild is invisible to Kill — it keeps running.
            //   * Old sync code: process.WaitForExit() hung forever waiting for stream EOF.
            //     Fix was process.Close() during cancel-cleanup.
            //   * New async code: WaitForExitAsync ignores streams, so this doesn't hang.
            //     SafelyWaitForAllOutput bounds the post-await drain to 5s per stream. Pipes
            //     are released by Process.Dispose at end of method (NOT during cancel-cleanup
            //     — see DoOurBestToCleanUp in SilentProcessRunner.cs for why adding Close back
            //     causes a 10-minute hang).
            //
            // This test asserts cancel returns in well under 30s in the grandchild scenario.

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

                    await WaitForPidFileAsync(grandchildPidFile, TimeSpan.FromSeconds(30));

                    var sw = Stopwatch.StartNew();
                    cts.Cancel();

                    // Cancel should return within ~10s worst case (5s SafelyWaitForAllOutput timeout
                    // per stream). If we hit the 30s test timeout, something is hanging — most likely
                    // process.Close() got re-added to DoOurBestToCleanUp (see that method for why).
                    var completed = task.Wait(TimeSpan.FromSeconds(30));
                    sw.Stop();

                    completed.Should().BeTrue(
                        $"ExecuteCommandAsync should return promptly after cancellation even when a Unix " +
                        $"grandchild (reparented to init/launchd) holds the redirected pipes. Worst case " +
                        $"is ~10s. If we hit the 30s test timeout, either process.Close() was re-introduced " +
                        $"in DoOurBestToCleanUp (which races with the Exited event WaitForExitAsync depends " +
                        $"on) or SafelyWaitForAllOutput's per-stream timeout has been removed. Elapsed " +
                        $"since cancel: {sw.Elapsed.TotalSeconds:F1}s");
                }
            }
            finally
            {
                TryKillGrandchild(grandchildPidFile);
            }
        }

        [Test]
        public async Task AbandonToken_ShouldReturnAbandonedExitCodeWithoutKillingProcess()
        {
            using var tempDir = new TemporaryDirectory();
            var pidFile = Path.Combine(tempDir.DirectoryPath, "process.pid");

            var abandonCommand = PlatformDetection.IsRunningOnWindows ? "powershell.exe" : "/bin/bash";
            var arguments = PlatformDetection.IsRunningOnWindows
                ? $"-NoProfile -NonInteractive -Command \"$PID | Out-File -FilePath '{pidFile}' -Encoding ASCII; Start-Sleep -Seconds 300\""
                : $"-c \"echo $$ > '{pidFile}' && sleep 300\"";

            using var cancelCts = new CancellationTokenSource();
            using var abandonCts = new CancellationTokenSource();

            var infoMessages = new StringBuilder();

            var task = Task.Run(async () => await SilentProcessRunner.ExecuteCommandAsync(
                abandonCommand,
                arguments,
                Environment.CurrentDirectory,
                debug: _ => { },
                info: msg => { lock (infoMessages) infoMessages.AppendLine(msg); },
                error: _ => { },
                customEnvironmentVariables: null,
                cancel: cancelCts.Token,
                abandon: abandonCts.Token));

            // Wait deterministically for the process to write its PID before we abandon
            await WaitForPidFileAsync(pidFile, TimeSpan.FromSeconds(30));
            abandonCts.Cancel();

            try
            {
                var exitCode = await task;

                // AbandonedExitCode is only returned from the abandon catch block, which
                // requires the abandon token to fire. If we'd accidentally waited for the
                // process to exit naturally, exitCode would be the script's own exit code,
                // not this sentinel. The exit code is the abandon contract.
                exitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
                infoMessages.ToString().Should().Contain("Tentacle has abandoned this script");

                // Whether the script keeps running doesn't matter in prod. We check it here so we
                // know our fixture successfully prevented it from being killed (the exit code matches either way).
                var sleepPid = int.Parse(SafelyReadAllText(pidFile).Trim());
                Process.GetProcessById(sleepPid).HasExited.Should().BeFalse("abandon should leave the underlying script process running");
            }
            finally
            {
                // Force-kill the sleeping process to avoid leaking it on CI
                if (File.Exists(pidFile) && int.TryParse(SafelyReadAllText(pidFile).Trim(), out var pid) && pid > 0)
                {
                    try { Process.GetProcessById(pid).Kill(); } catch { /* already gone */ }
                }
            }
        }

        static async Task WaitForPidFileAsync(string pidFile, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(pidFile) && int.TryParse(SafelyReadAllText(pidFile).Trim(), out var pid) && pid > 0)
                    return;
                await Task.Delay(50);
            }
            throw new TimeoutException(
                $"Test setup failed: a valid PID was never written to '{pidFile}'. " +
                $"The scenario under test is not being exercised.");
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

            // Why this is sync: Execute is a test helper that returns int and uses
            // out parameters — both force the signature to be sync. It's invoked
            // directly from sync NUnit test methods.
            //
            // Why blocking on the async call is safe: NUnit dispatches us on a
            // worker thread with no SynchronizationContext.
            //
            // Why low risk: this is test code. The worst case for a wrong call here
            // is a hung test, not a production incident.
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
