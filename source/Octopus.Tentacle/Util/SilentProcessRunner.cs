using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Util
{
    public static class SilentProcessRunner
    {
        public static CmdResult ExecuteCommand(this CommandLineInvocation invocation)
            => ExecuteCommand(invocation, Environment.CurrentDirectory);

        public static CmdResult ExecuteCommand(this CommandLineInvocation invocation, string workingDirectory)
        {
            if (workingDirectory == null)
                throw new ArgumentNullException(nameof(workingDirectory));

            var arguments = $"{invocation.Arguments} {invocation.SystemArguments ?? string.Empty}";
            var infos = new List<string>();
            var errors = new List<string>();

            var exitCode = ExecuteCommand(
                invocation.Executable,
                arguments,
                workingDirectory,
                infos.Add,
                errors.Add
            );

            return new CmdResult(exitCode, infos, errors);
        }

        public static int ExecuteCommand(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> info,
            Action<string> error,
            CancellationToken cancel = default)
            => ExecuteCommand(executable,
                arguments,
                workingDirectory,
                LogFileOnlyLogger.Current.Info,
                info,
                error,
                cancel);

        public static int ExecuteCommand(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            CancellationToken cancel = default)
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
                        if (running) DoOurBestToCleanUp(process, debug, error);
                    }))
                    {
                        if (cancel.IsCancellationRequested)
                            DoOurBestToCleanUp(process, debug,  error);

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        process.WaitForExit();

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
            try
            {
                //5 seconds is a bit arbitrary, but the process should have already exited by now, so unwise to wait too long
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

        static void DoOurBestToCleanUp(Process process, Action<string> debug, Action<string> error)
        {
            try
            {
                Hitman.TryKillProcessAndChildrenRecursively(process, debug);
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
            public static void TryKillProcessAndChildrenRecursively(Process process, Action<string> debug)
            {
                if (PlatformDetection.IsRunningOnNix)
                    TryKillLinuxProcessAndChildrenRecursively(process, debug);
                else if (PlatformDetection.IsRunningOnWindows)
                    TryKillWindowsProcessAndChildrenRecursively(process.Id, debug);
                else
                    throw new Exception("Unknown platform, unable to kill process");
            }

            static void TryKillLinuxProcessAndChildrenRecursively(Process process, Action<string> debug)
            {
                debug($"Attempting to kill Linux process and children recursively: {process.Id}");
                var result = ExecuteCommand(new CommandLineInvocation("/bin/bash", $"-c \"kill -TERM {process.Id}\""));
                result.Validate();
                //process.Kill() doesnt seem to work in netcore 2.2 there have been some improvments in netcore 3.0 as well as also allowing to kill child processes
                //https://github.com/dotnet/corefx/pull/34147
                //In netcore 2.2 if the process is terminated we still get stuck on process.WaitForExit(); we need to manually check to see if the process has exited and then close it.
                if (process.HasExited)
                {
                    debug($"Closing process to clean up resources: {process.Id}");
                    process.Close();
                }
                else
                {
                    debug($"Process hadn't exited for some reason: {process.Id}");
                }
            }

            static void TryKillWindowsProcessAndChildrenRecursively(int pid, Action<string> debug)
            {
                try
                {
                    debug($"Attempting to kill Windows process and children recursively via management objects: {pid}");

                    using (var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid))
                    {
                        using (var moc = searcher.Get())
                        {
                            foreach (var mo in moc.OfType<ManagementObject>())
                                TryKillWindowsProcessAndChildrenRecursively(Convert.ToInt32(mo["ProcessID"]), debug);
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
                    debug($"Attempting to kill Windows process via .Kill(): {pid}");

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