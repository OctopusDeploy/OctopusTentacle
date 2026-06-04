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

                        var waitForExit = Task.Run(() =>
                        {
                            try { process.WaitForExit(); }
                            catch { /* swalloe exceptions thrown when released by Process.Dispose in DoOurBestToCleanUp */ }
                        });

                        // Wait for the process to exit, but break if abandon cancallation token fires.
                        // Cancel kills then Close()s the process (via cancel.Register) so this returns.
                        // If the script is not actually stuck when abandon fires, abandoning will be in
                        // a race with the script to complete, if it wins, we return -48 and leave
                        // the process running.
                        await Task.WhenAny(waitForExit, WaitForAbandon(abandon)).ConfigureAwait(false);

                        if (abandon.IsCancellationRequested)
                        {
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

        static Task WaitForAbandon(CancellationToken token)
        {
            var tcs = new TaskCompletionSource<object?>();
            token.Register(() => tcs.TrySetResult(null));
            return tcs.Task;
        }

        static void SafelyWaitForAllOutput(ManualResetEventSlim outputResetEvent,
            CancellationToken cancel,
            Action<string> debug)
        {
            // Wait up to 5s for stream EOF (a null DataReceived marks it). The process has already
            // exited, so EOF is normally immediate. If a re-parented grandchild process is still holding
            // the pipe open then EOF will never come. This is why we time out and skip the final flush.
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
            // Cancel the readers so a late callback can't write to the workspace log after it's
            // disposed (that would throw). Used by both the normal and abandon paths.
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
            finally
            {
                try
                {
                    // Close the handles after the kill, on this thread. Releases the redirected pipes
                    // so the WaitForExit above returns even when a re-parented grandchild holds them open.
                    process.Close();
                }
                catch (Exception closeException)
                {
                    error($"Failed to close process resources: {closeException.Message}");
                }
            }
        }

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