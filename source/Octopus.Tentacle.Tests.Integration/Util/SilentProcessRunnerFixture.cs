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
        public async Task CancelThenAbandon_WhenGrandchildHoldsRedirectedPipes_StopsWaiting()
        {
            using var tempDir = new TemporaryDirectory();
            var grandchildPidFile = Path.Combine(tempDir.DirectoryPath, "grandchild.pid");

            // The scenario:
            //   * We launch a process that spawns a child, which spawns a long-running grandchild
            //     and then immediately exits.
            //   * Because the child exited BEFORE our cancel fires, the grandchild has been
            //     re-parented (PPID broken). Process.Kill(entireProcessTree:true) follows PPID
            //     links, so it does NOT find the grandchild — Kill kills nothing beyond the
            //     (already-dead) child.
            //   * The grandchild inherited our redirected stdout/stderr pipes and holds them open,
            //     so the stream readers never see EOF and WaitForExitAsync never completes on its own.
            //
            // Under this design only `abandon` breaks that wait — cancel does not. So cancel alone
            // cannot stop such a script; the caller must abandon, which returns AbandonedExitCode and
            // leaves the grandchild running. This test asserts cancel alone keeps waiting, then abandon
            // stops it promptly.
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
                using (var cancelCts = new CancellationTokenSource())
                using (var abandonCts = new CancellationTokenSource())
                {
                    var task = Task.Run(async () => await SilentProcessRunner.ExecuteCommandAsync(
                        "powershell.exe",
                        $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
                        "",
                        debug: _ => { },
                        info: _ => { },
                        error: _ => { },
                        customEnvironmentVariables: null,
                        cancel: cancelCts.Token,
                        abandon: abandonCts.Token));

                    // Wait for the grandchild to actually be spawned before cancelling
                    await WaitForPidFileAsync(grandchildPidFile, TimeSpan.FromSeconds(60));

                    // Cancel alone cannot stop the wait: Kill(entireProcessTree) can't reach the
                    // re-parented grandchild, and the grandchild holds our redirected pipes open so
                    // stream EOF never arrives. WaitForExitAsync keeps waiting — until abandon.
                    cancelCts.Cancel();
                    var stoppedByCancel = task.Wait(TimeSpan.FromSeconds(5));
                    stoppedByCancel.Should().BeFalse(
                        "cancel cannot stop a script whose re-parented grandchild holds the redirected " +
                        "pipes open; abandon is required to stop waiting");

                    // Abandon stops the wait promptly and returns the distinct exit code, leaving the
                    // grandchild running (pipes released by Process.Dispose at end of method).
                    abandonCts.Cancel();
                    var stoppedByAbandon = task.Wait(TimeSpan.FromSeconds(30));
                    stoppedByAbandon.Should().BeTrue(
                        "abandon should stop the wait promptly even while the grandchild holds the pipes");
                    (await task).Should().Be(ScriptExitCodes.AbandonedExitCode);
                }
            }
            finally
            {
                TryKillGrandchild(grandchildPidFile);
            }
        }

        [Test]
        public async Task CancelThenAbandon_WhenUnixGrandchildHoldsRedirectedPipes_StopsWaiting()
        {
            if (PlatformDetection.IsRunningOnWindows)
                Assert.Ignore("Unix-only repro (Mac/Linux). The Windows equivalent is covered by the [WindowsTest] above.");

            //   * We start sh, which backgrounds a long sleep (the grandchild) and exits immediately.
            //     The grandchild is re-parented to init/launchd and inherits our redirected
            //     stdout/stderr pipes, holding them open — so stream EOF never arrives.
            //   * Process.Kill(entireProcessTree:true) follows PPID links, so by cancel time the
            //     orphaned grandchild is invisible to Kill — it keeps running and keeps the pipes open.
            //
            // Under this design cancel does NOT break the wait — only `abandon` does. So cancelling a
            // script whose grandchild holds the pipes cannot stop it on its own; the caller must
            // abandon, which returns AbandonedExitCode and leaves the grandchild running. This test
            // asserts exactly that: cancel alone keeps waiting, then abandon stops it promptly.

            using var tempDir = new TemporaryDirectory();
            var grandchildPidFile = Path.Combine(tempDir.DirectoryPath, "grandchild.pid");
            var grandchild = $"sleep 600 & echo $! > '{grandchildPidFile}'; exit 0";

            try
            {
                using (var cancelCts = new CancellationTokenSource())
                using (var abandonCts = new CancellationTokenSource())
                {
                    var task = Task.Run(async () => await SilentProcessRunner.ExecuteCommandAsync(
                        "/bin/sh",
                        $"-c \"{grandchild}\"", // backgrounds the grandchild; `sh` does not wait for it.
                        "",
                        debug: _ => { },
                        info: _ => { },
                        error: _ => { },
                        customEnvironmentVariables: null,
                        cancel: cancelCts.Token,
                        abandon: abandonCts.Token));

                    await WaitForPidFileAsync(grandchildPidFile, TimeSpan.FromSeconds(60));

                    // Cancel alone cannot stop the wait: Kill(entireProcessTree) can't reach the
                    // re-parented grandchild, and the grandchild holds our redirected pipes open so
                    // stream EOF never arrives. WaitForExitAsync keeps waiting — until abandon.
                    cancelCts.Cancel();
                    var stoppedByCancel = task.Wait(TimeSpan.FromSeconds(5));
                    stoppedByCancel.Should().BeFalse(
                        "cancel cannot stop a script whose re-parented grandchild holds the redirected " +
                        "pipes open; abandon is required to stop waiting");

                    // Abandon stops the wait promptly and returns the distinct exit code, leaving the
                    // grandchild running (pipes released by Process.Dispose at end of method).
                    abandonCts.Cancel();
                    var stoppedByAbandon = task.Wait(TimeSpan.FromSeconds(30));
                    stoppedByAbandon.Should().BeTrue(
                        "abandon should stop the wait promptly even while the grandchild holds the pipes");
                    (await task).Should().Be(ScriptExitCodes.AbandonedExitCode);
                }
            }
            finally
            {
                TryKillGrandchild(grandchildPidFile);
            }
        }

        [Test]
        public async Task AbandonToken_ReturnsAbandonedExitCode()
        {
            // Abandon stops waiting and returns the distinct AbandonedExitCode. Abandon also
            // best-effort-kills the process — the kill itself is asserted by
            // ClientScriptExecutionAbandon.AbandonScript_WithNoPriorCancel_KillsTheProcess, and the
            // un-killable (survives) case by AbandonScript_WhenCancelFailsToKillProcess. (We do NOT
            // disable kill here: setting that env var is process-wide and leaks into the Tentacle
            // subprocesses other integration tests spawn.)
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
                exitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
                infoMessages.ToString().Should().Contain("Tentacle has abandoned this script");
            }
            finally
            {
                // Force-kill the script process if it's somehow still around, to avoid leaking on CI
                if (File.Exists(pidFile) && int.TryParse(SafelyReadAllText(pidFile).Trim(), out var pid) && pid > 0)
                {
                    try { Process.GetProcessById(pid).Kill(); } catch { /* already gone */ }
                }
            }
        }

        [Test]
        public async Task AbandonToken_WhenCancelAlsoRequested_StillReturnsAbandonedExitCode()
        {
            // Once abandon is requested, the runner returns AbandonedExitCode even though cancel is
            // also requested — the abandon branch resolves the wait and returns -48.
            //
            // We fire abandon FIRST so this is deterministic. The exit code is NOT deterministic if
            // cancel and abandon race on a *killable* process: cancel's kill makes WaitForExitAsync
            // complete with the killed exit code before abandon is observed. That race does not happen
            // in production — cancel and abandon arrive as separate, sequential RPCs, and the server
            // only sends abandon AFTER cancel has failed to unstick the script. The deterministic
            // both-tokens case (cancel fired, kill genuinely fails, abandon -> -48) is covered
            // end-to-end by ClientScriptExecutionAbandon.AbandonScript_WhenCancelFailsToKillProcess.
            using var tempDir = new TemporaryDirectory();
            var pidFile = Path.Combine(tempDir.DirectoryPath, "process.pid");

            var executable = PlatformDetection.IsRunningOnWindows ? "powershell.exe" : "/bin/bash";
            var arguments = PlatformDetection.IsRunningOnWindows
                ? $"-NoProfile -NonInteractive -Command \"$PID | Out-File -FilePath '{pidFile}' -Encoding ASCII; Start-Sleep -Seconds 300\""
                : $"-c \"echo $$ > '{pidFile}' && sleep 300\"";

            using var cancelCts = new CancellationTokenSource();
            using var abandonCts = new CancellationTokenSource();

            var task = Task.Run(async () => await SilentProcessRunner.ExecuteCommandAsync(
                executable,
                arguments,
                Environment.CurrentDirectory,
                debug: _ => { },
                info: _ => { },
                error: _ => { },
                customEnvironmentVariables: null,
                cancel: cancelCts.Token,
                abandon: abandonCts.Token));

            await WaitForPidFileAsync(pidFile, TimeSpan.FromSeconds(30));

            // Abandon first (resolves the wait to -48), then cancel is also requested.
            abandonCts.Cancel();
            cancelCts.Cancel();

            try
            {
                var exitCode = await task;
                exitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
            }
            finally
            {
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
