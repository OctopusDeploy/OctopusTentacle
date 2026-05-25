using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;

namespace Octopus.Tentacle.Util
{
    public static class SilentProcessRunner
    {
        public static Task<int> ExecuteCommandAsync(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            CancellationToken cancel,
            CancellationToken abandon)
        {
            return ExecuteCommandAsync(executable, arguments, workingDirectory, debug, info, error, customEnvironmentVariables: null, cancel: cancel, abandon: abandon);
        }

        public static async Task<int> ExecuteCommandAsync(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            IReadOnlyDictionary<string, string>? customEnvironmentVariables = null,
            CancellationToken cancel = default,
            CancellationToken abandon = default)
        {
            if (executable == null)
                throw new ArgumentNullException(nameof(executable));
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));
            if (workingDirectory == null)
                throw new ArgumentNullException(nameof(workingDirectory));
            if (debug == null)
                throw new ArgumentNullException(nameof(debug));
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            void WriteData(Action<string> action, ManualResetEventSlim resetEvent, DataReceivedEventArgs e)
            {
                try
                {
                    if (e.Data == null)
                    {
                        resetEvent.Set();
                        return;
                    }

                    action(e.Data);
                }
                catch (Exception ex)
                {
                    try
                    {
                        error($"Error occurred handling message: {ex.PrettyPrint()}");
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            try
            {
                // We need to be careful to make sure the message is accurate otherwise people could wrongly assume the exe is in the working directory when it could be somewhere completely different!
                var executableDirectoryName = Path.GetDirectoryName(executable);
                debug($"Executable directory is {executableDirectoryName}");

                var exeInSamePathAsWorkingDirectory = string.Equals(executableDirectoryName?.TrimEnd('\\', '/'), workingDirectory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
                var exeFileNameOrFullPath = exeInSamePathAsWorkingDirectory ? Path.GetFileName(executable) : executable;
                debug($"Executable name or full path: {exeFileNameOrFullPath}");

                var encoding = EncodingDetector.GetOEMEncoding();

                debug($"Starting {exeFileNameOrFullPath} in working directory '{workingDirectory}' using '{encoding.EncodingName}' encoding running as '{ProcessIdentity.CurrentUserName}'");

                using (var outputResetEvent = new ManualResetEventSlim(false))
                using (var errorResetEvent = new ManualResetEventSlim(false))
                using (var process = new Process())
                {
                    process.StartInfo.FileName = executable;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.WorkingDirectory = workingDirectory;

                    if (customEnvironmentVariables != null && customEnvironmentVariables.Any())
                    {
                        // Note this will add to the environment variables, potentially replacing existing ones.
                        foreach (var variable in customEnvironmentVariables)
                        {
                            process.StartInfo.EnvironmentVariables[variable.Key] = variable.Value;
                        }
                    }

                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.EnableRaisingEvents = true;
                    if (PlatformDetection.IsRunningOnWindows)
                    {
                        process.StartInfo.StandardOutputEncoding = encoding;
                        process.StartInfo.StandardErrorEncoding = encoding;
                    }

                    process.OutputDataReceived += (sender, e) =>
                    {
                        WriteData(info, outputResetEvent, e);
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        WriteData(error, errorResetEvent, e);
                    };

                    process.Start();

                    var running = true;

                    using (cancel.Register(() =>
                           {
                               if (running) DoOurBestToCleanUp(process, error);
                           }))
                    {
                        if (cancel.IsCancellationRequested)
                            DoOurBestToCleanUp(process, error);

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        try
                        {
                            // WaitForExitAsync completes when the Process.Exited event fires (or
                            // when `abandon` cancels). Unlike the sync WaitForExit() no-timeout
                            // overload, it does NOT wait for the redirected stdout/stderr streams
                            // to reach EOF — so a re-parented grandchild holding our pipes open
                            // cannot hang us here. Stream draining is handled separately below by
                            // SafelyWaitForAllOutput (with a 5s timeout per stream).
                            //
                            // We pass `abandon` (not `cancel`) because cancel is handled via the
                            // cancel.Register callback above which kills the process tree; the
                            // resulting Exited event is what unblocks this await on cancel.
                            // `abandon` is a separate token used by EFT-3295 to stop waiting
                            // WITHOUT killing the process — see the catch block below.
#if NETFRAMEWORK
                            await WaitForExitAsyncNetFramework(process, abandon).ConfigureAwait(false);
#else
                            await process.WaitForExitAsync(abandon).ConfigureAwait(false);
#endif
                        }
                        catch (OperationCanceledException) when (abandon.IsCancellationRequested && !process.HasExited)
                        {
                            info("Tentacle has abandoned this script. The underlying script process may still be running on this host.");
                            SafelyCancelRead(process.CancelErrorRead, debug);
                            SafelyCancelRead(process.CancelOutputRead, debug);
                            running = false;
                            return ScriptExitCodes.AbandonedExitCode;
                        }

                        SafelyCancelRead(process.CancelErrorRead, debug);
                        SafelyCancelRead(process.CancelOutputRead, debug);

                        SafelyWaitForAllOutput(outputResetEvent, cancel, debug);
                        SafelyWaitForAllOutput(errorResetEvent, cancel, debug);

                        var exitCode = SafelyGetExitCode(process);
                        debug($"Process {exeFileNameOrFullPath} in {workingDirectory} exited with code {exitCode}");

                        running = false;
                        return exitCode;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error when attempting to execute {executable}: {ex.Message}", ex);
            }
        }

        static int SafelyGetExitCode(Process process)
        {
            try
            {
                return process.ExitCode;
            }
            catch (InvalidOperationException ex) 
                when (ex.Message == "No process is associated with this object." || 
                      ex.Message == "Process was not started by this object, so requested information cannot be determined.")
            {
                return -1;
            }
        }

        static void SafelyWaitForAllOutput(ManualResetEventSlim outputResetEvent,
            CancellationToken cancel,
            Action<string> debug)
        {
            // Waits for the OutputDataReceived/ErrorDataReceived handler to signal EOF on the
            // stream (it sets the reset event when it receives a null DataReceivedEventArgs.Data,
            // which is .NET's EOF marker). This does NOT close the pipe — it just gives the OS
            // up to 5 seconds to deliver the EOF.
            //
            // If a re-parented grandchild is holding the pipe open, EOF never arrives, the wait
            // times out, and we proceed without the final flush of buffered output. The pipe is
            // released later by Process.Dispose() at end of ExecuteCommandAsync via the
            // `using (var process = new Process())` block.
            //
            // 5 seconds is somewhat arbitrary — the process has already exited by the time we
            // reach here, so under normal circumstances EOF arrives within milliseconds.
            try
            {
                outputResetEvent.Wait(TimeSpan.FromSeconds(5), cancel);
            }
            catch (OperationCanceledException ex)
            {
                debug($"Swallowing {ex.GetType().Name} while waiting for last of the process output.");
            }
        }

        static void SafelyCancelRead(Action action, Action<string> debug)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException ex)
            {
                debug($"Swallowing {ex.GetType().Name} calling {action.Method.Name}.");
            }
        }

        static void DoOurBestToCleanUp(Process process, Action<string> error)
        {
            try
            {
                Hitman.TryKillProcessAndChildrenRecursively(process);
            }
            catch (Exception hitmanException)
            {
                error($"Failed to kill the launched process and its children: {hitmanException}");
                try
                {
                    process.Kill();
                }
                catch (Exception killProcessException)
                {
                    error($"Failed to kill the launched process: {killProcessException}");
                }
            }
            // Do NOT add process.Close() here. The pre-async version of this code did, and adding
            // it back will cause cancel to hang forever. Here's the full picture:
            //
            // OLD SYNC CODE: the calling thread blocked inside process.WaitForExit() (no-timeout
            // overload), which waits for BOTH the process to exit AND the redirected stream
            // readers to reach EOF. If a re-parented grandchild held our stdout/stderr open, the
            // stream readers never reached EOF, so WaitForExit() blocked forever. Calling
            // process.Close() during cancel-cleanup forced the Process object to release its
            // handles to the redirected pipes, which made the readers see EOF, which let
            // WaitForExit() return. That's why Close() was here.
            //
            // NEW ASYNC CODE: the calling thread awaits a TaskCompletionSource that completes
            // when the Process.Exited event fires. WaitForExitAsync does NOT wait on the
            // redirected streams (Microsoft confirms in the docs: "output processing will not
            // have completed when this method returns"). So a grandchild holding pipes open
            // can't hang the await. The original reason for Close() is gone.
            //
            // WHY ADDING Close() BACK IS WORSE THAN USELESS: process.Close() detaches the Process
            // object from the underlying OS process, which tears down the wait state that
            // produces the Exited event. If Close() runs before the kernel has signalled the
            // exit to .NET (which is asynchronous — Hitman.Kill returns immediately, the OS
            // delivers the exit notification some time later), the Exited event never fires,
            // our TCS never completes, and the await hangs forever. Every cancel races.
            //
            // HOW PIPES ACTUALLY GET RELEASED NOW:
            //   1. After WaitForExitAsync returns, SafelyWaitForAllOutput waits up to 5 seconds
            //      per stream for EOF. If a grandchild holds the pipes, this times out and we
            //      proceed (it bounds cancel latency; it does NOT close anything).
            //   2. The outer `using (var process = new Process())` block calls Process.Dispose
            //      at end of method, which calls Close internally. Because we're no longer
            //      awaiting WaitForExitAsync at this point, the Close-vs-Exited race can't
            //      happen — the wait state is already torn down by our code, not by Close.
            //
            // Worst case cancel latency with grandchild holding pipes: ~10s (5s × 2 streams).
            // Covered by tests in SilentProcessRunnerFixture:
            //   - CancellationToken_WhenGrandchildHoldsRedirectedPipes_ShouldNotHang (Windows)
            //   - CancellationToken_WhenUnixGrandchildHoldsRedirectedPipes_ShouldNotHang (Unix)
            // Both assert cancel returns within 30s in this scenario.
        }

#if NETFRAMEWORK
        // WaitForExitAsync is not available on .NET Framework 4.x; polyfill using Process.Exited event + TaskCompletionSource.
        static Task WaitForExitAsyncNetFramework(Process process, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenRegistration registration = default;

            void OnExited(object? sender, EventArgs e)
            {
                registration.Dispose();
                tcs.TrySetResult(null);
            }

            process.Exited += OnExited;

            // Guard against race: process may have already exited before we subscribed.
            if (process.HasExited)
            {
                tcs.TrySetResult(null);
            }

            if (cancellationToken.CanBeCanceled)
            {
                registration = cancellationToken.Register(() =>
                {
                    process.Exited -= OnExited;
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return tcs.Task;
        }
#endif

        [DllImport("kernel32.dll", SetLastError = true)]
#pragma warning disable PC003 // Native API not available in UWP
        static extern bool GetCPInfoEx([MarshalAs(UnmanagedType.U4)]
            int codePage,
            [MarshalAs(UnmanagedType.U4)]
            int dwFlags,
            out CPINFOEX lpCPInfoEx);
#pragma warning restore PC003 // Native API not available in UWP

        class Hitman
        {
            public static void TryKillProcessAndChildrenRecursively(Process process)
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill)))
                {
                    // Test-only no-op: simulate "kill was attempted but didn't terminate the process".
                    // Only activated when the test harness sets this env var on the Tentacle process.
                    return;
                }

#if NETFRAMEWORK
                TryKillWindowsProcessAndChildrenRecursively(process.Id);
#endif
#if !NETFRAMEWORK
                // Since .NET Core 3.0 there is support for killing a process and it's children
                process.Kill(true);
#endif
            }

#if NET8_0_OR_GREATER
            [SupportedOSPlatform("windows")]
#endif
            static void TryKillWindowsProcessAndChildrenRecursively(int pid)
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid))
                    {
                        using (var moc = searcher.Get())
                        {
                            foreach (var mo in moc.OfType<ManagementObject>())
                                TryKillWindowsProcessAndChildrenRecursively(Convert.ToInt32(mo["ProcessID"]));
                        }
                    }
                }
                catch (MarshalDirectiveException)
                {
                    // This is a known framework bug: https://github.com/dotnet/runtime/issues/28840
                    //
                    // The ManagementObjectSearcher netcore3.1 is completely broken. It's possible to crash it just by creating
                    // a new instance of ManagementScope. Once this framework bug is addressed, we should be able to remove
                    // this catch block.
                    //
                    // Unfortunately, this means that we have no feasible way to recursively kill processes under netcore, so
                    // we're left with just killing the top-level process and hoping that the others terminate soon.
                }

                try
                {
                    var proc = Process.GetProcessById(pid);
                    proc.Kill();
                }
                catch (ArgumentException)
                {
                    // Process already exited.
                }
            }
        }

        internal class EncodingDetector
        {
            public static Encoding GetOEMEncoding()
            {
                var defaultEncoding = Encoding.UTF8;

                if (PlatformDetection.IsRunningOnWindows)
                    try
                    {
                        // Get the OEM CodePage for the installation, otherwise fall back to code page 850 (DOS Western Europe)
                        // https://en.wikipedia.org/wiki/Code_page_850
                        const int CP_OEMCP = 1;
                        const int dwFlags = 0;
                        const int CodePage850 = 850;

                        var codepage = GetCPInfoEx(CP_OEMCP, dwFlags, out var info) ? info.CodePage : CodePage850;

#if REQUIRES_CODE_PAGE_PROVIDER
                        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(codepage); // When it says that this can return null, it *really can* return null.
                        return encoding ?? defaultEncoding;
#else
                        var encoding = Encoding.GetEncoding(codepage);
                        return encoding ?? Encoding.UTF8;
#endif
                    }
                    catch
                    {
                        // Fall back to UTF8 if everything goes wrong
                        return defaultEncoding;
                    }

                return defaultEncoding;
            }
        }

        // ReSharper disable InconsistentNaming
        const int MAX_DEFAULTCHAR = 2;
        const int MAX_LEADBYTES = 12;
        const int MAX_PATH = 260;

        // ReSharper disable MemberCanBePrivate.Local
        // ReSharper disable once StructCanBeMadeReadOnly
        [StructLayout(LayoutKind.Sequential)]
        struct CPINFOEX
        {
            [MarshalAs(UnmanagedType.U4)]
            public readonly int MaxCharSize;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DEFAULTCHAR)]
            public readonly byte[] DefaultChar;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_LEADBYTES)]
            public readonly byte[] LeadBytes;

            public readonly char UnicodeDefaultChar;

            [MarshalAs(UnmanagedType.U4)]
            public readonly int CodePage;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public readonly string CodePageName;
        }
        // ReSharper restore MemberCanBePrivate.Local
        // ReSharper restore InconsistentNaming
    }
}