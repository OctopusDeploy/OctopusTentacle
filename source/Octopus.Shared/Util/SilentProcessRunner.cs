using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if CAN_FIND_CHILD_PROCESSES
using System.Management;
#endif
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Util
{
    public static class SilentProcessRunner
    {
        public static int ExecuteCommand(this CommandLineInvocation invocation, ILog log)
        {
            return ExecuteCommand(invocation, Environment.CurrentDirectory, log);
        }

        public static int ExecuteCommand(this CommandLineInvocation invocation, string workingDirectory, ILog log)
        {
            var arguments = (invocation.Arguments ?? "") + " " + (invocation.SystemArguments ?? "");

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
        {
            return ExecuteCommand(invocation, Environment.CurrentDirectory);
        }

        public static CmdResult ExecuteCommand(this CommandLineInvocation invocation, string workingDirectory)
        {
            var arguments = (invocation.Arguments ?? "") + " " + (invocation.SystemArguments ?? "");
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
            NetworkCredential runAs = default(NetworkCredential),
            IDictionary<string, string> customEnvironmentVariables = null,
            CancellationToken cancel = default(CancellationToken))
        {
            return ExecuteCommand(executable, arguments, workingDirectory, Log.System().Info, info, error, runAs, customEnvironmentVariables, cancel);
        }

        public static int ExecuteCommand(
            string executable,
            string arguments,
            string workingDirectory,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            NetworkCredential runAs = default(NetworkCredential),
            IDictionary<string, string> customEnvironmentVariables = null,
            CancellationToken cancel = default(CancellationToken))
        {
            try
            {
                // We need to be careful to make sure the message is accurate otherwise people could wrongly assume the exe is in the working directory when it could be somewhere completely different!
                var exeInSamePathAsWorkingDirectory = string.Equals(Path.GetDirectoryName(executable).TrimEnd('\\', '/'), workingDirectory.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
                var exeFileNameOrFullPath = exeInSamePathAsWorkingDirectory ? Path.GetFileName(executable) : executable;
                var runAsSameUser = runAs == default(NetworkCredential);
                var runningAs = runAsSameUser ? $@"{WindowsIdentity.GetCurrent().Name}" : $@"{runAs.Domain ?? Environment.MachineName}\{runAs.UserName}";
                var hasCustomEnvironmentVariables = customEnvironmentVariables != null && customEnvironmentVariables.Any();
                var customEnvironmentVars =
                    hasCustomEnvironmentVariables
                    ? (runAsSameUser ? $"the same environment variables as the launching process plus {customEnvironmentVariables.Count} custom variable(s)" : $"that user's environment variables plus {customEnvironmentVariables.Count} custom variable(s)")
                    : (runAsSameUser ? "the same environment variables as the launching process" : "that user's default environment variables");
                var encoding = EncodingDetector.GetOEMEncoding();
                debug($"Starting {exeFileNameOrFullPath} in working directory '{workingDirectory}' using '{encoding.EncodingName}' encoding running as '{runningAs}' with {customEnvironmentVars}");
                using (var process = new Process())
                {
                    process.StartInfo.FileName = executable;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.WorkingDirectory = workingDirectory;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    if (runAs != default(NetworkCredential))
                    {
                        RunAsDifferentUser(process.StartInfo, runAs, customEnvironmentVariables);
                    }
                    else
                    {
                        RunAsSameUser(process.StartInfo, customEnvironmentVariables);
                    }
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.StandardOutputEncoding = encoding;
                    process.StartInfo.StandardErrorEncoding = encoding;

                    using (var outputWaitHandle = new AutoResetEvent(false))
                    using (var errorWaitHandle = new AutoResetEvent(false))
                    {
                        process.OutputDataReceived += (sender, e) =>
                        {
                            try
                            {
                                if (e.Data == null)
                                    outputWaitHandle.Set();
                                else
                                    info(e.Data);
                            }
                            catch (Exception ex)
                            {
                                try
                                {
                                    error($"Error occured handling message: {ex.PrettyPrint()}");
                                }
                                catch
                                {
                                    // Ignore
                                }
                            }
                        };

                        process.ErrorDataReceived += (sender, e) =>
                        {
                            try
                            {
                                if (e.Data == null)
                                    errorWaitHandle.Set();
                                else
                                    error(e.Data);
                            }
                            catch (Exception ex)
                            {
                                try
                                {
                                    error($"Error occured handling message: {ex.PrettyPrint()}");
                                }
                                catch
                                {
                                    // Ignore
                                }
                            }
                        };

                        process.Start();

                        var running = true;

                        cancel.Register(() =>
                        {
                            if (!running)
                                return;
                            DoOurBestToCleanUp(process, error);
                        });

                        if (cancel.IsCancellationRequested)
                        {
                            DoOurBestToCleanUp(process, error);
                        }

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        process.WaitForExit();

                        debug($"Process {exeFileNameOrFullPath} in {workingDirectory} exited with code {process.ExitCode}");

                        running = false;

                        outputWaitHandle.WaitOne();
                        errorWaitHandle.WaitOne();

                        return process.ExitCode;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error when attempting to execute {executable}: {ex.Message}", ex);
            }
        }

        public static void ExecuteCommandWithoutWaiting(
            string executable,
            string arguments,
            string workingDirectory,
            NetworkCredential runAs = default(NetworkCredential),
            IDictionary<string, string> customEnvironmentVariables = null)
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = executable;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.WorkingDirectory = workingDirectory;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    if (runAs != default(NetworkCredential))
                    {
                        RunAsDifferentUser(process.StartInfo, runAs, customEnvironmentVariables);
                    }
                    else
                    {
                        RunAsSameUser(process.StartInfo, customEnvironmentVariables);
                    }

                    process.Start();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error when attempting to execute {executable}: {ex.Message}", ex);
            }
        }

        private static void RunAsSameUser(ProcessStartInfo processStartInfo, IDictionary<string, string> customEnvironmentVariables)
        {
            // Accessing the ProcessStartInfo.EnvironmentVariables dictionary will pre-load the environment variables for the current process
            // Then we'll add/overwrite with the customEnvironmentVariables
            if (customEnvironmentVariables != null && customEnvironmentVariables.Any())
            {
                foreach (var variable in customEnvironmentVariables)
                {
                    processStartInfo.EnvironmentVariables[variable.Key] = variable.Value;
                }
            }
        }

        private static void RunAsDifferentUser(ProcessStartInfo startInfo, NetworkCredential runAs, IDictionary<string, string> customEnvironmentVariables)
        {
            startInfo.Domain = runAs.Domain;
            startInfo.UserName = runAs.UserName;
            startInfo.Password = runAs.SecurePassword;
            startInfo.LoadUserProfile = true;

            WindowStationAndDesktopAccess.GrantAccessToWindowStationAndDesktop(runAs.UserName, runAs.Domain);

            if (customEnvironmentVariables != null && customEnvironmentVariables.Any())
            {
                SetEnvironmentVariablesForTargetUser(startInfo, runAs, customEnvironmentVariables);
            }
        }

        private static void SetEnvironmentVariablesForTargetUser(ProcessStartInfo startInfo, NetworkCredential runAs, IDictionary<string, string> customEnvironmentVariables)
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
            {
                targetUserEnvironmentVariables[variable.Key] = variable.Value;
            }

            // Starting from a clean slate, copy the resulting environment variables into the ProcessStartInfo
            startInfo.EnvironmentVariables.Clear();
            foreach (var variable in targetUserEnvironmentVariables)
            {
                startInfo.EnvironmentVariables[variable.Key] = variable.Value;
            }
        }

        private static readonly Dictionary<string, Dictionary<string, string>> EnvironmentVariablesForUserCache = new Dictionary<string, Dictionary<string, string>>();
        
        private static Dictionary<string, string> GetTargetUserEnvironmentVariables(NetworkCredential runAs)
        {
            var cacheKey = $"{runAs.Domain}\\{runAs.UserName}";
            if (EnvironmentVariablesForUserCache.TryGetValue(cacheKey, out var cached))
                return cached;

            // We don't really need to worry about locking, multiple initialization shouldn't be a problem since the result should always be the same
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

        static void DoOurBestToCleanUp(Process process, Action<string> error)
        {
            try
            {
                Hitman.TryKillProcessAndChildrenRecursively(process.Id);
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

        internal class EncodingDetector
        {
            public static Encoding GetOEMEncoding()
            {
                try
                {
                    // Get the OEM CodePage for the installation, otherwise fall back to code page 850 (DOS Western Europe)
                    // https://en.wikipedia.org/wiki/Code_page_850
                    const int CP_OEMCP = 1;
                    const int dwFlags = 0;
                    const int CodePage850 = 850;

                    return Encoding.GetEncoding(GetCPInfoEx(CP_OEMCP, dwFlags, out var info) ? info.CodePage : CodePage850);
                }
                catch
                {
                    // Fall back to UTF8 if everything goes wrong
                    return Encoding.UTF8;
                }
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool GetCPInfoEx([MarshalAs(UnmanagedType.U4)] int codePage, [MarshalAs(UnmanagedType.U4)] int dwFlags, out CPINFOEX lpCPInfoEx);

            const int MAX_DEFAULTCHAR = 2;
            const int MAX_LEADBYTES = 12;
            const int MAX_PATH = 260;

            [StructLayout(LayoutKind.Sequential)]
            public struct CPINFOEX
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
        }

        internal class Hitman
        {
            public static void TryKillProcessAndChildrenRecursively(int pid)
            {
#if CAN_FIND_CHILD_PROCESSES
                using (var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid))
                {
                    using (var moc = searcher.Get())
                    {
                        foreach (var mo in moc.OfType<ManagementObject>())
                        {
                            TryKillProcessAndChildrenRecursively(Convert.ToInt32(mo["ProcessID"]));
                        }
                    }
                }
#endif

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

        // Required to allow a service to run a process as another user
        // See http://stackoverflow.com/questions/677874/starting-a-process-with-credentials-from-a-windows-service/30687230#30687230
        internal class WindowStationAndDesktopAccess
        {
            public static void GrantAccessToWindowStationAndDesktop(string username, string domainName = null)
            {
                var hWindowStation = Win32Helper.Invoke(() => GetProcessWindowStation(), "Failed to get a handle to the current window station for this process");
                const int windowStationAllAccess = 0x000f037f;
                GrantAccess(username, domainName, hWindowStation, windowStationAllAccess);
                var hDesktop = Win32Helper.Invoke(() => GetThreadDesktop(GetCurrentThreadId()), "Failed to the a handle to the desktop for the current thread");
                const int desktopRightsAllAccess = 0x000f01ff;
                GrantAccess(username, domainName, hDesktop, desktopRightsAllAccess);
            }

            static void GrantAccess(string username, string domainName, IntPtr handle, int accessMask)
            {
                SafeHandle safeHandle = new NoopSafeHandle(handle);
                var security =
                    new GenericSecurity(
                        false, ResourceType.WindowObject, safeHandle, AccessControlSections.Access);

                var account = string.IsNullOrEmpty(domainName)
                    ? new NTAccount(username)
                    : new NTAccount(domainName, username);

                security.AddAccessRule(
                    new GenericAccessRule(
                        account, accessMask, AccessControlType.Allow));
                security.Persist(safeHandle, AccessControlSections.Access);
            }

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr GetProcessWindowStation();

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr GetThreadDesktop(int dwThreadId);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern int GetCurrentThreadId();

            // All the code to manipulate a security object is available in .NET framework,
            // but its API tries to be type-safe and handle-safe, enforcing a special implementation
            // (to an otherwise generic WinAPI) for each handle type. This is to make sure
            // only a correct set of permissions can be set for corresponding object types and
            // mainly that handles do not leak.
            // Hence the AccessRule and the NativeObjectSecurity classes are abstract.
            // This is the simplest possible implementation that yet allows us to make use
            // of the existing .NET implementation, sparing necessity to
            // P/Invoke the underlying WinAPI.
            private class GenericAccessRule : AccessRule
            {
                public GenericAccessRule(
                    IdentityReference identity, int accessMask, AccessControlType type) :
                    base(identity, accessMask, false, InheritanceFlags.None,
                        PropagationFlags.None, type)
                {
                }
            }

            private class GenericSecurity : NativeObjectSecurity
            {
                public GenericSecurity(
                    bool isContainer, ResourceType resType, SafeHandle objectHandle,
                    AccessControlSections sectionsRequested)
                    : base(isContainer, resType, objectHandle, sectionsRequested)
                {
                }

                public new void Persist(SafeHandle handle, AccessControlSections includeSections)
                {
                    base.Persist(handle, includeSections);
                }

                public new void AddAccessRule(AccessRule rule)
                {
                    base.AddAccessRule(rule);
                }

                public override Type AccessRightType => throw new NotImplementedException();

                public override AccessRule AccessRuleFactory(
                    IdentityReference identityReference,
                    int accessMask, bool isInherited, InheritanceFlags inheritanceFlags,
                    PropagationFlags propagationFlags, AccessControlType type)
                    => throw new NotImplementedException();

                public override Type AccessRuleType => typeof(AccessRule);

                public override AuditRule AuditRuleFactory(
                    IdentityReference identityReference, int accessMask,
                    bool isInherited, InheritanceFlags inheritanceFlags,
                    PropagationFlags propagationFlags, AuditFlags flags)
                    => throw new NotImplementedException();

                public override Type AuditRuleType => typeof(AuditRule);
            }

            // Handles returned by GetProcessWindowStation and GetThreadDesktop should not be closed
            private class NoopSafeHandle : SafeHandle
            {
                public NoopSafeHandle(IntPtr handle) :
                    base(handle, false)
                {
                }

                public override bool IsInvalid => false;

                protected override bool ReleaseHandle()
                {
                    return true;
                }
            }
        }

        internal class AccessToken : IDisposable
        {
            public string Username { get; }
            public SafeAccessTokenHandle Handle { get; }

            private AccessToken(string username, SafeAccessTokenHandle handle)
            {
                Username = username;
                Handle = handle;
            }

            public static AccessToken Logon(string username, string password, string domain = ".", LogonType logonType = LogonType.Network, LogonProvider logonProvider = LogonProvider.Default)
            {
                // See https://msdn.microsoft.com/en-us/library/windows/desktop/aa378184(v=vs.85).aspx
                var hToken = IntPtr.Zero;
                Win32Helper.Invoke(() => LogonUser(username, domain, password, LogonType.Network, LogonProvider.Default, out hToken),
                    $"Logon failed for the user '{username}'");

                return new AccessToken(username, new SafeAccessTokenHandle(hToken));
            }

            public void Dispose()
            {
                Handle?.Dispose();
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

            public enum LogonProvider
            {
                Default = 0,
                WinNT40 = 2,
                WinNT50 = 3,
            }

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern bool LogonUser(string username, string domain, string password, LogonType logonType, LogonProvider logonProvider, out IntPtr hToken);
        }

        internal class UserProfile : IDisposable
        {
            readonly AccessToken token;
            readonly SafeRegistryHandle userProfile;

            private UserProfile(AccessToken token, SafeRegistryHandle userProfile)
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

            [DllImport("userenv.dll", SetLastError = true)]
            static extern bool LoadUserProfile(SafeAccessTokenHandle hToken, ref PROFILEINFO lpProfileInfo);

            [DllImport("userenv.dll", SetLastError = true)]
            static extern bool UnloadUserProfile(SafeAccessTokenHandle hToken, SafeRegistryHandle hProfile);

            [StructLayout(LayoutKind.Sequential)]
            struct PROFILEINFO
            {
                public int dwSize;
                public int dwFlags;
                public string lpUserName;
                public string lpProfilePath;
                public string lpDefaultPath;
                public string lpServerName;
                public string lpPolicyPath;
                public IntPtr hProfile;
            }
        }

        private class Win32Helper
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
                                {
                                    userEnvironment.Add(data, "");
                                }
                                else if (index == data.Length - 1)
                                {
                                    userEnvironment.Add(data.Substring(0, index), "");
                                }
                                else
                                {
                                    userEnvironment.Add(data.Substring(0, index), data.Substring(index + 1));
                                }
                                testData.Length = 0;
                            }
                            if (*current == 0 && current != start && *(current - 1) == 0)
                            {
                                done = true;
                            }
                            if (*current != 0)
                            {
                                testData.Append((char)*current);
                            }
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

            [DllImport("userenv.dll", SetLastError = true)]
            private static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, SafeAccessTokenHandle hToken, bool inheritFromCurrentProcess);

            [DllImport("userenv.dll", SetLastError = true)]
            private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);
        }

        internal sealed class SafeAccessTokenHandle : SafeHandle
        {
            // 0 is an Invalid Handle
            public SafeAccessTokenHandle(IntPtr handle) : base(handle, true) { }

            public static SafeAccessTokenHandle InvalidHandle => new SafeAccessTokenHandle(IntPtr.Zero);

            public override bool IsInvalid => handle == IntPtr.Zero || handle == new IntPtr(-1);

            protected override bool ReleaseHandle()
            {
                return CloseHandle(handle);
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool CloseHandle(IntPtr hHandle);
        }
    }
}