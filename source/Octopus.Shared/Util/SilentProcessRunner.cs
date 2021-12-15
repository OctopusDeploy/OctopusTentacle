using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Octopus.Diagnostics;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Util
{
    public static class SilentProcessRunner
    {
        static readonly object EnvironmentVariablesCacheLock = new object();
        static IDictionary mostRecentMachineEnvironmentVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
        static readonly Dictionary<string, Dictionary<string, string>> EnvironmentVariablesForUserCache = new Dictionary<string, Dictionary<string, string>>();

        public static int ExecuteCommand(this CommandLineInvocation invocation, ILog log)
            => ExecuteCommand(invocation, Environment.CurrentDirectory, log);

        public static int ExecuteCommand(this CommandLineInvocation invocation, string workingDirectory, ILog log)
        {
            var arguments = $"{invocation.Arguments} {invocation.SystemArguments ?? string.Empty}";

            var exitCode = ExecuteCommand(
                invocation.Executable,
                arguments,
                workingDirectory,
                log.Info,
                log.Error
            );

            return exitCode;
        }

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
            NetworkCredential? runAs = null,
            IDictionary<string, string>? customEnvironmentVariables = null,
            CancellationToken cancel = default)
            => ExecuteCommand(executable,
                arguments,
                workingDirectory,
                LogFileOnlyLogger.Current.Info,
                info,
                error,
                runAs,
                customEnvironmentVariables,
                cancel);

        public static int ExecuteCommand(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            NetworkCredential? runAs = null,
            IDictionary<string, string>? customEnvironmentVariables = null,
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

            customEnvironmentVariables = customEnvironmentVariables == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(customEnvironmentVariables);

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
                var hasCustomEnvironmentVariables = customEnvironmentVariables.Any();

                bool runAsSameUser;
                string runningAs;
                if (runAs == null)
                {
                    debug("No user context provided. Running as current user.");
                    runAsSameUser = true;
                    runningAs = $@"{ProcessIdentity.CurrentUserName}";
                }
                else
                {
                    runAsSameUser = false;
                    runningAs = $@"{runAs.Domain ?? Environment.MachineName}\{runAs.UserName}";
                    debug($"Different user context provided. Running as {runningAs}");
                }

                var customEnvironmentVarsMessage = hasCustomEnvironmentVariables
                    ? runAsSameUser
                        ? $"the same environment variables as the launching process plus {customEnvironmentVariables.Count} custom variable(s)"
                        : $"that user's environment variables plus {customEnvironmentVariables.Count} custom variable(s)"
                    : runAsSameUser
                        ? "the same environment variables as the launching process"
                        : "that user's default environment variables";

                debug($"Starting {exeFileNameOrFullPath} in working directory '{workingDirectory}' using '{encoding.EncodingName}' encoding running as '{runningAs}' with {customEnvironmentVarsMessage}");

                using (var outputResetEvent = new ManualResetEventSlim(false))
                using (var errorResetEvent = new ManualResetEventSlim(false))
                using (var process = new Process())
                {
                    process.StartInfo.FileName = executable;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.WorkingDirectory = workingDirectory;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    if (runAs == null)
                        RunAsSameUser(process.StartInfo, customEnvironmentVariables);
                    else if (PlatformDetection.IsRunningOnWindows)
                        RunAsDifferentUser(process.StartInfo, runAs, customEnvironmentVariables);
                    else
                        throw new PlatformNotSupportedException("NetCore on Linux or Mac does not support running a process as a different user.");

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

        public static void ExecuteCommandWithoutWaiting(
            string executable,
            string arguments,
            string workingDirectory,
            NetworkCredential? runAs = null,
            IDictionary<string, string>? customEnvironmentVariables = null)
        {
            customEnvironmentVariables = customEnvironmentVariables == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(customEnvironmentVariables);

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = executable;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.WorkingDirectory = workingDirectory;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    if (runAs == null)
                        RunAsSameUser(process.StartInfo, customEnvironmentVariables);
                    else if (PlatformDetection.IsRunningOnWindows)
                        RunAsDifferentUser(process.StartInfo, runAs, customEnvironmentVariables);
                    else
                        throw new PlatformNotSupportedException("NetCore on linux or Mac does not support running a process as a different user.");

                    process.Start();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error when attempting to execute {executable}: {ex.Message}", ex);
            }
        }

        static void RunAsSameUser(ProcessStartInfo processStartInfo, IDictionary<string, string>? customEnvironmentVariables)
        {
            // Accessing the ProcessStartInfo.EnvironmentVariables dictionary will pre-load the environment variables for the current process
            // Then we'll add/overwrite with the customEnvironmentVariables
            if (customEnvironmentVariables != null && customEnvironmentVariables.Any())
                foreach (var variable in customEnvironmentVariables)
                    processStartInfo.EnvironmentVariables[variable.Key] = variable.Value;
        }

        static void RunAsDifferentUser(ProcessStartInfo startInfo, NetworkCredential runAs, IDictionary<string, string>? customEnvironmentVariables)
        {
#pragma warning disable PC001 // API not supported on all platforms
            startInfo.UserName = runAs.UserName;
            startInfo.Domain = runAs.Domain;
            startInfo.Password = runAs.SecurePassword;
            startInfo.LoadUserProfile = true;
#pragma warning restore PC001 // API not supported on all platforms

            WindowStationAndDesktopAccess.GrantAccessToWindowStationAndDesktop(runAs.UserName, runAs.Domain);

            if (customEnvironmentVariables != null && customEnvironmentVariables.Any())
                SetEnvironmentVariablesForTargetUser(startInfo, runAs, customEnvironmentVariables);
        }

        static void SetEnvironmentVariablesForTargetUser(ProcessStartInfo startInfo, NetworkCredential runAs, IDictionary<string, string> customEnvironmentVariables)
        {
            // Double check before we go doing p/invoke gymnastics
            if (customEnvironmentVariables == null || !customEnvironmentVariables.Any()) return;

            // If ProcessStartInfo.enviromentVariables (field) is null, the new process will build its environment variables from scratch
            // This will be the system environment variables, plus the user's profile variables (if the user profile is loaded)
            // However, if the ProcessStartInfo.environmentVariables (field) is not null, these environment variables will be used instead
            // As soon as we touch ProcessStartInfo.EnvironmentVariables (property) it lazy loads the environment variables for the current process
            // which in turn means the launched process will get the environment variables for the wrong user profile!

            // See https://msdn.microsoft.com/en-us/library/windows/desktop/ms682425(v=vs.85).aspx (CreateProcess) used when ProcessStartInfo.Username is not set
            // See https://msdn.microsoft.com/en-us/library/windows/desktop/ms682431(v=vs.85).aspx (CreateProcessWithLogonW) used when ProcessStartInfo.Username is set

            // Start by getting the environment variables for the target user (as if they started a process themselves)
            // This will get the system environment variables along with the user's profile variables
            var targetUserEnvironmentVariables = GetTargetUserEnvironmentVariables(runAs);

            // Now copy in the extra environment variables we want to propagate from this process
            foreach (var variable in customEnvironmentVariables)
                targetUserEnvironmentVariables[variable.Key] = variable.Value;

            // Starting from a clean slate, copy the resulting environment variables into the ProcessStartInfo
            startInfo.EnvironmentVariables.Clear();
            foreach (var variable in targetUserEnvironmentVariables)
                startInfo.EnvironmentVariables[variable.Key] = variable.Value;
        }

        static Dictionary<string, string> GetTargetUserEnvironmentVariables(NetworkCredential runAs)
        {
            var cacheKey = $"{runAs.Domain}\\{runAs.UserName}";

            // Start with a pessimistic lock until such a time where we want more throughput for concurrent processes
            // In the real world we shouldn't have too many processes wanting to run concurrently
            lock (EnvironmentVariablesCacheLock)
            {
                // If the machine environment variables have changed we should invalidate the entire cache
                InvalidateEnvironmentVariablesForUserCacheIfMachineEnvironmentVariablesHaveChanged();

                // Otherwise the cache will generally be valid, except for the (hopefully) rare case where a variable was added/changed for the specific user
                if (EnvironmentVariablesForUserCache.TryGetValue(cacheKey, out var cached))
                    return cached;

                Dictionary<string, string> targetUserEnvironmentVariables;
                using (var token = AccessToken.Logon(runAs.UserName, runAs.Password, runAs.Domain))
                using (var userProfile = UserProfile.Load(token))
                {
                    targetUserEnvironmentVariables = EnvironmentBlock.GetEnvironmentVariablesForUser(token, false);
                    userProfile.Unload();
                }

                // Cache the target user's environment variables so we don't have to load them every time
                // The downside is that once we target a certain user account, their variables are snapshotted in time
                EnvironmentVariablesForUserCache[cacheKey] = targetUserEnvironmentVariables;
                return targetUserEnvironmentVariables;
            }
        }

        static void InvalidateEnvironmentVariablesForUserCacheIfMachineEnvironmentVariablesHaveChanged()
        {
            var currentMachineEnvironmentVariables = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine);
            var machineEnvironmentVariablesHaveChanged =
#pragma warning disable DE0006 // API is deprecated
                !currentMachineEnvironmentVariables.Cast<DictionaryEntry>()
                    .OrderBy(e => e.Key)
                    .SequenceEqual(mostRecentMachineEnvironmentVariables.Cast<DictionaryEntry>().OrderBy(e => e.Key));
#pragma warning restore DE0006 // API is deprecated

            if (machineEnvironmentVariablesHaveChanged)
            {
                mostRecentMachineEnvironmentVariables = currentMachineEnvironmentVariables;
                EnvironmentVariablesForUserCache.Clear();
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
                if (PlatformDetection.IsRunningOnNix)
                    TryKillLinuxProcessAndChildrenRecursively(process);
                else if (PlatformDetection.IsRunningOnWindows)
                    TryKillWindowsProcessAndChildrenRecursively(process.Id);
                else
                    throw new Exception("Unknown platform, unable to kill process");
            }

            static void TryKillLinuxProcessAndChildrenRecursively(Process process)
            {
                var result = ExecuteCommand(new CommandLineInvocation("/bin/bash", $"-c \"kill -TERM {process.Id}\""));
                result.Validate();
                //process.Kill() doesnt seem to work in netcore 2.2 there have been some improvments in netcore 3.0 as well as also allowing to kill child processes
                //https://github.com/dotnet/corefx/pull/34147
                //In netcore 2.2 if the process is terminated we still get stuck on process.WaitForExit(); we need to manually check to see if the process has exited and then close it.
                if (process.HasExited)
                    process.Close();
            }

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

        // Required to allow a service to run a process as another user
        // See http://stackoverflow.com/questions/677874/starting-a-process-with-credentials-from-a-windows-service/30687230#30687230
        internal class WindowStationAndDesktopAccess
        {
            public static void GrantAccessToWindowStationAndDesktop(string username, string? domainName = null)
            {
                var hWindowStation = Win32Helper.Invoke(() => GetProcessWindowStation(), "Failed to get a handle to the current window station for this process");
                const int windowStationAllAccess = 0x000f037f;
                GrantAccess(username, domainName, hWindowStation, windowStationAllAccess);
                var hDesktop = Win32Helper.Invoke(() => GetThreadDesktop(GetCurrentThreadId()), "Failed to the a handle to the desktop for the current thread");
                const int desktopRightsAllAccess = 0x000f01ff;
                GrantAccess(username, domainName, hDesktop, desktopRightsAllAccess);
            }

            static void GrantAccess(string username, string? domainName, IntPtr handle, int accessMask)
            {
                SafeHandle safeHandle = new NoopSafeHandle(handle);
                var security =
                    new GenericSecurity(
                        false,
                        ResourceType.WindowObject,
                        safeHandle,
                        AccessControlSections.Access);

                var account = string.IsNullOrEmpty(domainName)
#pragma warning disable PC001 // API not supported on all platforms
                    ? new NTAccount(username)
                    : new NTAccount(domainName, username);
#pragma warning restore PC001 // API not supported on all platforms

                security.AddAccessRule(
                    new GenericAccessRule(
                        account,
                        accessMask,
                        AccessControlType.Allow));
                security.Persist(safeHandle, AccessControlSections.Access);
            }

            // Native API not available in UWP
            // All the code to manipulate a security object is available in .NET framework,
            // but its API tries to be type-safe and handle-safe, enforcing a special implementation
            // (to an otherwise generic WinAPI) for each handle type. This is to make sure
            // only a correct set of permissions can be set for corresponding object types and
            // mainly that handles do not leak.
            // Hence the AccessRule and the NativeObjectSecurity classes are abstract.
            // This is the simplest possible implementation that yet allows us to make use
            // of the existing .NET implementation, sparing necessity to
            // P/Invoke the underlying WinAPI.
            class GenericAccessRule : AccessRule
            {
                public GenericAccessRule(
                    IdentityReference identity,
                    int accessMask,
                    AccessControlType type) :
                    base(identity,
                        accessMask,
                        false,
                        InheritanceFlags.None,
                        PropagationFlags.None,
                        type)
                {
                }
            }

            class GenericSecurity : NativeObjectSecurity
            {
                public GenericSecurity(
                    bool isContainer,
                    ResourceType resType,
                    SafeHandle objectHandle,
                    AccessControlSections sectionsRequested)
                    : base(isContainer, resType, objectHandle, sectionsRequested)
                {
                }

                public override Type AccessRightType => throw new NotImplementedException();

                public override Type AccessRuleType => typeof(AccessRule);

                public override Type AuditRuleType => typeof(AuditRule);

                public new void Persist(SafeHandle handle, AccessControlSections includeSections)
                {
#pragma warning disable PC001 // API not supported on all platforms
                    base.Persist(handle, includeSections);
#pragma warning restore PC001 // API not supported on all platforms
                }

                public new void AddAccessRule(AccessRule rule)
                {
#pragma warning disable PC001 // API not supported on all platforms
                    base.AddAccessRule(rule);
#pragma warning restore PC001 // API not supported on all platforms
                }

                public override AccessRule AccessRuleFactory(
                    IdentityReference identityReference,
                    int accessMask,
                    bool isInherited,
                    InheritanceFlags inheritanceFlags,
                    PropagationFlags propagationFlags,
                    AccessControlType type)
                    => throw new NotImplementedException();

                public override AuditRule AuditRuleFactory(
                    IdentityReference identityReference,
                    int accessMask,
                    bool isInherited,
                    InheritanceFlags inheritanceFlags,
                    PropagationFlags propagationFlags,
                    AuditFlags flags)
                    => throw new NotImplementedException();
            }

            // Handles returned by GetProcessWindowStation and GetThreadDesktop should not be closed
            class NoopSafeHandle : SafeHandle
            {
                public NoopSafeHandle(IntPtr handle) :
                    base(handle, false)
                {
                }

                public override bool IsInvalid => false;

                protected override bool ReleaseHandle()
                    => true;
            }

#pragma warning disable PC003 // Native API not available in UWP
            [DllImport("user32.dll", SetLastError = true)]
            static extern IntPtr GetProcessWindowStation();

            [DllImport("user32.dll", SetLastError = true)]
            static extern IntPtr GetThreadDesktop(int dwThreadId);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern int GetCurrentThreadId();
#pragma warning restore PC003
        }

        internal class AccessToken : IDisposable
        {
            public enum LogonProvider
            {
                Default = 0,
                WinNT40 = 2,
                WinNT50 = 3
            }

            public enum LogonType
            {
                Interactive = 2,
                Network = 3,
                Batch = 4,
                Service = 5,
                Unlock = 7,
                NetworkClearText = 8,
                NewCredentials = 9
            }

            AccessToken(string username, SafeAccessTokenHandle handle)
            {
                Username = username;
                Handle = handle;
            }

            public string Username { get; }
            public SafeAccessTokenHandle Handle { get; }

            public static AccessToken Logon(string username,
                string password,
                string domain = ".",
                LogonType logonType = LogonType.Network,
                LogonProvider logonProvider = LogonProvider.Default)
            {
                // See https://msdn.microsoft.com/en-us/library/windows/desktop/aa378184(v=vs.85).aspx
                var hToken = IntPtr.Zero;
                Win32Helper.Invoke(() => LogonUser(username,
                        domain,
                        password,
                        LogonType.Network,
                        LogonProvider.Default,
                        out hToken),
                    $"Logon failed for the user '{username}'");

                return new AccessToken(username, new SafeAccessTokenHandle(hToken));
            }

            public void Dispose()
            {
                Handle?.Dispose();
            }

            [DllImport("advapi32.dll", SetLastError = true)]
#pragma warning disable PC003 // Native API not available in UWP
            static extern bool LogonUser(string username,
                string domain,
                string password,
                LogonType logonType,
                LogonProvider logonProvider,
                out IntPtr hToken);
#pragma warning restore PC003 // Native API not available in UWP
        }

        internal class UserProfile : IDisposable
        {
            readonly AccessToken token;
            readonly SafeRegistryHandle userProfile;

            UserProfile(AccessToken token, SafeRegistryHandle userProfile)
            {
                this.token = token;
                this.userProfile = userProfile;
            }

            public static UserProfile Load(AccessToken token)
            {
                var userProfile = new PROFILEINFO
                {
                    lpUserName = token.Username
                };
                userProfile.dwSize = Marshal.SizeOf(userProfile);

                // See https://msdn.microsoft.com/en-us/library/windows/desktop/bb762281(v=vs.85).aspx
                Win32Helper.Invoke(() => LoadUserProfile(token.Handle, ref userProfile),
                    $"Failed to load user profile for user '{token.Username}'");

                return new UserProfile(token, new SafeRegistryHandle(userProfile.hProfile, false));
            }

            public void Unload()
            {
                // See https://msdn.microsoft.com/en-us/library/windows/desktop/bb762282(v=vs.85).aspx
                // This function closes the registry handle for the user profile too
                Win32Helper.Invoke(() => UnloadUserProfile(token.Handle, userProfile),
                    $"Failed to unload user profile for user '{token.Username}'");
            }

            public void Dispose()
            {
                if (userProfile != null && !userProfile.IsClosed)
                {
                    try
                    {
                        Unload();
                    }
                    catch
                    {
                        // Don't throw in dispose method
                    }

                    userProfile.Dispose();
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            struct PROFILEINFO
            {
                public int dwSize;
                public readonly int dwFlags;
                public string lpUserName;
                public readonly string lpProfilePath;
                public readonly string lpDefaultPath;
                public readonly string lpServerName;
                public readonly string lpPolicyPath;
                public readonly IntPtr hProfile;
            }

#pragma warning disable PC003 // Native API not available in UWP
            [DllImport("userenv.dll", SetLastError = true)]
            static extern bool LoadUserProfile(SafeAccessTokenHandle hToken, ref PROFILEINFO lpProfileInfo);

            [DllImport("userenv.dll", SetLastError = true)]
            static extern bool UnloadUserProfile(SafeAccessTokenHandle hToken, SafeRegistryHandle hProfile);
#pragma warning restore PC003 // Native API not available in UWP
        }

        class Win32Helper
        {
            public static bool Invoke(Func<bool> nativeMethod, string failureDescription)
            {
                try
                {
                    return nativeMethod() ? true : throw new Win32Exception();
                }
                catch (Win32Exception ex)
                {
                    throw new Exception($"{failureDescription}: {ex.Message}", ex);
                }
            }

            public static T Invoke<T>(Func<T> nativeMethod, Func<T, bool> successful, string failureDescription)
            {
                try
                {
                    var result = nativeMethod();
                    return successful(result) ? result : throw new Win32Exception();
                }
                catch (Win32Exception ex)
                {
                    throw new Exception($"{failureDescription}: {ex.Message}", ex);
                }
            }

            public static IntPtr Invoke(Func<IntPtr> nativeMethod, string failureDescription)
            {
                try
                {
                    var result = nativeMethod();
                    return result != IntPtr.Zero ? result : throw new Win32Exception();
                }
                catch (Win32Exception ex)
                {
                    throw new Exception($"{failureDescription}: {ex.Message}", ex);
                }
            }
        }

        internal class EnvironmentBlock
        {
            internal static Dictionary<string, string> GetEnvironmentVariablesForUser(AccessToken token, bool inheritFromCurrentProcess)
            {
                var env = IntPtr.Zero;

                // See https://msdn.microsoft.com/en-us/library/windows/desktop/bb762270(v=vs.85).aspx
                Win32Helper.Invoke(() => CreateEnvironmentBlock(out env, token.Handle, inheritFromCurrentProcess),
                    $"Failed to load the environment variables for the user '{token.Username}'");

                var userEnvironment = new Dictionary<string, string>();
                try
                {
                    var testData = new StringBuilder();
                    unsafe
                    {
                        // The environment block is an array of null-terminated Unicode strings.
                        // Key and Value are separated by =
                        // The list ends with two nulls (\0\0).
                        var start = (short*)env.ToPointer();
                        var done = false;
                        var current = start;
                        while (!done)
                        {
                            if (testData.Length > 0 && *current == 0 && current != start)
                            {
                                var data = testData.ToString();
                                var index = data.IndexOf('=');
                                if (index == -1)
                                    userEnvironment.Add(data, "");
                                else if (index == data.Length - 1)
                                    userEnvironment.Add(data.Substring(0, index), "");
                                else
                                    userEnvironment.Add(data.Substring(0, index), data.Substring(index + 1));
                                testData.Length = 0;
                            }

                            if (*current == 0 && current != start && *(current - 1) == 0)
                                done = true;
                            if (*current != 0)
                                testData.Append((char)*current);
                            current++;
                        }
                    }
                }
                finally
                {
                    // See https://msdn.microsoft.com/en-us/library/windows/desktop/bb762274(v=vs.85).aspx
                    Win32Helper.Invoke(() => DestroyEnvironmentBlock(env),
                        $"Failed to destroy the environment variables structure for user '{token.Username}'");
                }

                return userEnvironment;
            }

#pragma warning disable PC003 // Native API not available in UWP
            [DllImport("userenv.dll", SetLastError = true)]
            static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, SafeAccessTokenHandle hToken, bool inheritFromCurrentProcess);

            [DllImport("userenv.dll", SetLastError = true)]
            static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);
#pragma warning restore PC003 // Native API not available in UWP
        }

        internal sealed class SafeAccessTokenHandle : SafeHandle
        {
            // 0 is an Invalid Handle
            public SafeAccessTokenHandle(IntPtr handle) : base(handle, true)
            {
            }

            public static SafeAccessTokenHandle InvalidHandle => new SafeAccessTokenHandle(IntPtr.Zero);

            public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);

            protected override bool ReleaseHandle()
                => CloseHandle(handle);

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool CloseHandle(IntPtr hHandle);
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