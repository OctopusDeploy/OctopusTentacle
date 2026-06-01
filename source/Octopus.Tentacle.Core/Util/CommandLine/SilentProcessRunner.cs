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

                        // Only `abandon` breaks this wait — cancel does not.
                        //
                        // Cancel kills the process via cancel.Register; we then wait here for it to exit
                        // naturally, so a script that honours the kill returns its real exit code (e.g. 137
                        // for SIGKILL, 143 for SIGTERM). A script that will NOT exit — genuinely stuck, OR a
                        // re-parented grandchild holding our redirected stdout/stderr pipes open so stream
                        // EOF never arrives — keeps waiting here. The only way to stop waiting on such a
                        // script is for the caller to abandon it: the abandon token cancels this await.
                        //
                        // Unlike PR1, this async wait uses WaitForExitAsync (the Exited event), so Close()
                        // cannot be used to unblock the grandchild case — it races the Exited event the
                        // wait depends on. That is why grandchildren now REQUIRE abandon, not cancel alone.
                        try
                        {
                            await WaitForProcessExitAsync(process, abandon).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (abandon.IsCancellationRequested)
                        {
                            // Abandon best-effort-kills (anti-abuse): kill it if we can, then stop waiting
                            // and release. Doing the kill here — sequentially, in the abandon branch — is
                            // race-free. The kill is idempotent if cancel already ran it. The process
                            // survives abandon only when the kill genuinely can't land (stuck / re-parented
                            // grandchild). From the caller's perspective abandon is a race against natural
                            // exit, so returning AbandonedExitCode is acceptable even if the process happened
                            // to finish at the same moment — that's why we don't check process.HasExited.
                            DoOurBestToCleanUp(process, error);
                            info("Tentacle has abandoned this script. The underlying script process may still be running on this host.");
                            SafelyCancelOutputAndErrorRead(process, debug);
                            running = false;
                            return ScriptExitCodes.AbandonedExitCode;
                        }

                        SafelyCancelOutputAndErrorRead(process, debug);

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
            // outputResetEvent.Wait is waiting for the OutputDataReceived/ErrorDataReceived
            // handlers to signal EOF on the stream (when it receives a null
            // DataReceivedEventArgs.Data, .NET's EOF marker). This does NOT close the pipe,
            // it just gives the OS up to 5 seconds to deliver the EOF.
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

        static void SafelyCancelOutputAndErrorRead(Process process, Action<string> debug)
        {
            // Cancel the output/error readers so a late OutputDataReceived/ErrorDataReceived
            // callback doesn't try to write to a workspace log that's already been disposed by
            // the using-block above; that write would throw ObjectDisposedException. Called in
            // both the normal completion path and the abandon path; extracted here so the two
            // callers stay consistent.
            SafelyCancelRead(process.CancelErrorRead, debug);
            SafelyCancelRead(process.CancelOutputRead, debug);
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

            // NOTE: deliberately no process.Close() here. The async WaitForExitAsync wait depends on the
            // Process's Exited event; Close() tears that down (StopWatchingForExit) and races the wait's
            // completion. PR1 could Close() because it used a synchronous WaitForExit; this async path
            // cannot. The still-pending wait is released when the using(process) disposes on return.
        }

        // Single place we block waiting for the spawned process to exit.
        // On .NET Framework we use a TaskCompletionSource polyfill because
        // Process.WaitForExitAsync doesn't exist there; on .NET 8+ we use the
        // framework method directly.
        static Task WaitForProcessExitAsync(Process process, CancellationToken cancellationToken)
        {
#if NETFRAMEWORK
            return WaitForProcessExitAsyncNetFrameworkPolyfill(process, cancellationToken);
#else
            return process.WaitForExitAsync(cancellationToken);
#endif
        }

#if NETFRAMEWORK
        static Task WaitForProcessExitAsyncNetFrameworkPolyfill(Process process, CancellationToken cancellationToken)
        {
            // EnableRaisingEvents must be true for the process.Exited handler below to fire.
            // On .NET 8+ Process.WaitForExitAsync sets this itself; here on netframework we
            // have to set it ourselves before subscribing.
            // https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexitasync
            process.EnableRaisingEvents = true;

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
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill_UNSAFE_FOR_PRODUCTION)))
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