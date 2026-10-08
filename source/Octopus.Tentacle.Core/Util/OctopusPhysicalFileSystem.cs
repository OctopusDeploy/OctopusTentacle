using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Util;
using Polly;

namespace Octopus.Tentacle.Core.Util
{
    public class CorePhysicalFileSystem : IOctopusFileSystem
    {
        const long FiveHundredMegabytes = 500 * 1024 * 1024;

        public CorePhysicalFileSystem(ISystemLog log, bool isRunningAsKubernetesAgent)
        {
            Log = log;
            IsRunningAsKubernetesAgent = isRunningAsKubernetesAgent;
        }

        ISystemLog Log { get; }
        bool IsRunningAsKubernetesAgent;

        public bool FileExists(string path)
            => File.Exists(path);

        public bool DirectoryExists(string path)
            => Directory.Exists(path);

        public bool DirectoryIsEmpty(string path)
        {
            try
            {
                return !Directory.GetFileSystemEntries(path).Any();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to list directory contents");
                return false;
            }
        }

        public void DeleteFile(string path, DeletionOptions? options = null)
        {
            DeleteFile(path, CancellationToken.None, options).Wait();
        }

        public async Task DeleteFile(string path, CancellationToken cancellationToken, DeletionOptions? options = null)
        {
            options ??= DeletionOptions.TryThreeTimes;

            if (string.IsNullOrWhiteSpace(path))
                return;

            await TryToDoSomethingMultipleTimes(i =>
                {
                    if (File.Exists(path))
                    {
                        if (i > 1) // Did our first attempt fail?
                            File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }
                },
                options.RetryAttempts,
                options.SleepBetweenAttemptsMilliseconds,
                options.ThrowOnFailure,
                cancellationToken);
        }

        public void DeleteDirectory(string path, DeletionOptions? options = null)
        {
            DeleteDirectory(path, DefaultCancellationToken, options).Wait();
        }

        public async Task DeleteDirectory(string path, CancellationToken cancellationToken, DeletionOptions? options = null)
        {
            await PurgeDirectoryAsync(
                path,
                cancellationToken,
                includeTarget: true,
                options);
        }

        public IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns)
        {
            return searchPatterns.Length == 0
                ? Directory.EnumerateFiles(parentDirectoryPath, "*", SearchOption.TopDirectoryOnly)
                : searchPatterns.SelectMany(pattern => Directory.EnumerateFiles(parentDirectoryPath, pattern, SearchOption.TopDirectoryOnly));
        }

        public IEnumerable<string> EnumerateDirectories(string parentDirectoryPath)
        {
            if (!DirectoryExists(parentDirectoryPath))
                return Enumerable.Empty<string>();

            return Directory.EnumerateDirectories(parentDirectoryPath);
        }

        public long GetFileSize(string path)
            => new FileInfo(path).Length;

        public string ReadFile(string path, bool withRetry = true)
        {
            var content = Policy<string>
                .Handle<IOException>()
                .WaitAndRetry(withRetry ? 10 : 0, retryCount => TimeSpan.FromMilliseconds(100 * retryCount))
                .Execute(() => File.ReadAllText(path));

            return content;
        }

        public void AppendToFile(string path, string contents)
        {
            File.AppendAllText(path, contents);
        }

        public void OverwriteFile(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        public void OverwriteFile(string path, string contents, Encoding encoding)
        {
            File.WriteAllText(path, contents, encoding);
        }

        public void CopyFile(string source, string destination, bool overwrite)
        {
            File.Copy(source, destination, overwrite);
        }

        public Stream OpenFile(string path, FileAccess access, FileShare share)
            => OpenFile(path, FileMode.OpenOrCreate, access, share);

        public Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            try
            {
                return new FileStream(path, mode, access, share);
            }
            catch (UnauthorizedAccessException)
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Exists && (fileInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                    // Throw a more helpful message than .NET's
                    // System.UnauthorizedAccessException: Access to the path ... is denied.
                    throw new IOException(path + " is a directory not a file");
                throw;
            }
        }

        string GetTempBasePath()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            EnsureDirectoryExists(path);

            path = Path.Combine(path, Assembly.GetEntryAssembly() != null ? Assembly.GetEntryAssembly()!.GetName().Name! : "Octopus");
            return Path.Combine(path, "Temp");
        }

        public void CreateDirectory(string path)
        {
            if (Directory.Exists(path))
                return;
            Directory.CreateDirectory(path);
        }

        public string CreateTemporaryDirectory()
        {
            var path = Path.Combine(GetTempBasePath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        static readonly CancellationToken DefaultCancellationToken = CancellationToken.None;

        IEnumerable<string> DefaultFileEnumerationFunc(string target)
        {
            return EnumerateFiles(target);
        }

        async Task PurgeDirectoryAsync(
            string targetDirectory,
            CancellationToken? cancel,
            bool? includeTarget,
            DeletionOptions? options)
        {
            if (!DirectoryExists(targetDirectory))
                return;

            cancel ??= CancellationToken.None;
            includeTarget ??= false;
            options ??= DeletionOptions.TryThreeTimes;

            foreach (var file in DefaultFileEnumerationFunc(targetDirectory))
            {
                await DeleteFile(file, cancel.Value, options);
            }

            foreach (var directory in EnumerateDirectories(targetDirectory))
            {
                var info = new DirectoryInfo(directory);
                if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    await TryToDoSomethingMultipleTimes(
                        _ => info.Delete(true),
                        options.RetryAttempts,
                        options.SleepBetweenAttemptsMilliseconds,
                        options.ThrowOnFailure,
                        cancel.Value);
                else
                    await PurgeDirectoryAsync(
                        directory,
                        cancel,
                        true,
                        options);
            }

            if (includeTarget.Value)
            {
                await TryToDoSomethingMultipleTimes(
                    _ =>
                    {
                        if (DirectoryIsEmpty(targetDirectory))
                        {
                            var dirInfo = new DirectoryInfo(targetDirectory)
                            {
                                Attributes = FileAttributes.Normal
                            };
                            dirInfo.Delete(true);
                        }
                    },
                    options.RetryAttempts,
                    options.SleepBetweenAttemptsMilliseconds,
                    options.ThrowOnFailure,
                    cancel.Value);
            }
        }

        public void WriteAllBytes(string filePath, byte[] data)
        {
            File.WriteAllBytes(filePath, data);
        }

        public void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

#if NETFRAMEWORK
        // .NET Framework only runs on Windows, where there are no Unix permissions or owners to set.
        public void WriteAllTextOwnerOnly(string path, string contents)
            => File.WriteAllText(path, contents);

        public bool RestrictFilePermissionsToOwner(string path)
            => false;

        public bool TryCreateFileOwnerOnly(string path, string contents)
            => TryCreateFileOwnerOnly(path, new UTF8Encoding(false).GetBytes(contents));

        public bool TryCreateFileOwnerOnly(string path, byte[] contents)
        {
            var temporaryPath = TemporaryPathBeside(path);
            try
            {
                File.WriteAllBytes(temporaryPath, contents);
                try
                {
                    // MoveFile never replaces an existing file.
                    File.Move(temporaryPath, path);
                    return true;
                }
                catch (IOException) when (File.Exists(path))
                {
                    return false;
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public bool TryChangeOwnerToMatch(string path, string referencePath)
            => false;

        public bool TryChangeOwner(string path, string userName)
            => false;
#else
        const UnixFileMode OwnerReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        public void WriteAllTextOwnerOnly(string path, string contents)
        {
            if (OperatingSystem.IsWindows())
            {
                File.WriteAllText(path, contents);
                return;
            }

            // UnixCreateMode is only honoured when the file is created, so an existing file (which we may be replacing)
            // is tightened explicitly as well. Its old contents are truncated away by FileMode.Create.
            var options = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = OwnerReadWrite
            };
            using (var stream = new FileStream(path, options))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(contents);
            }

            File.SetUnixFileMode(path, OwnerReadWrite);
        }

        public bool RestrictFilePermissionsToOwner(string path)
        {
            if (OperatingSystem.IsWindows())
                return false;

            var current = File.GetUnixFileMode(path);
            if ((current & ~OwnerReadWrite) == UnixFileMode.None)
                return false;

            File.SetUnixFileMode(path, OwnerReadWrite);
            return true;
        }

        public bool TryCreateFileOwnerOnly(string path, string contents)
            => TryCreateFileOwnerOnly(path, new UTF8Encoding(false).GetBytes(contents));

        public bool TryCreateFileOwnerOnly(string path, byte[] contents)
        {
            var temporaryPath = TemporaryPathBeside(path);
            try
            {
                var options = new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None
                };
                if (!OperatingSystem.IsWindows())
                    options.UnixCreateMode = OwnerReadWrite;
                using (var stream = new FileStream(temporaryPath, options))
                {
                    stream.Write(contents, 0, contents.Length);
                    stream.Flush(flushToDisk: true);
                }

                if (!OperatingSystem.IsWindows())
                {
                    // link(2) cannot replace an existing file, so whichever process links first wins and every other one
                    // sees the complete winning file. File.Move(overwrite: false) is not atomic on Unix: .NET 8 checks
                    // that the destination is absent and then calls rename(2), which replaces whatever appeared in
                    // between, so two processes could each keep a different key. Only a file system without hard links
                    // falls through to that check-then-rename, as does anything else in the way (a directory, say),
                    // so that it fails the same way it always did.
                    if (link(temporaryPath, path) == 0)
                        return true;
                    if (Marshal.GetLastWin32Error() == EEXIST && File.Exists(path))
                        return false;
                }

                try
                {
                    File.Move(temporaryPath, path, overwrite: false);
                    return true;
                }
                catch (IOException) when (File.Exists(path))
                {
                    return false;
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        const int EEXIST = 17;

        [DllImport("libc", SetLastError = true)]
        static extern int link(string oldpath, string newpath);

        public bool TryChangeOwnerToMatch(string path, string referencePath)
        {
            // GNU coreutils has --reference; BSD (macOS) and BusyBox (musl images) do not, so fall back to reading the
            // owner with POSIX `ls -n` and passing it to chown as uid:gid, which every chown understands.
            if (RunChownAsRoot("--reference=" + referencePath, "--", path))
                return true;

            var owner = ReadNumericOwner(referencePath);
            return owner != null && RunChownAsRoot("--", owner, path);
        }

        public bool TryChangeOwner(string path, string userName)
            => RunChownAsRoot("--", userName, path);

        /// <summary>
        /// The owner of <paramref name="path"/> as <c>uid:gid</c>, from <c>ls -ldn</c>, which is POSIX and so the same on
        /// GNU, BSD and BusyBox. Null if it could not be read.
        /// </summary>
        string? ReadNumericOwner(string path)
        {
            var (exitCode, output, _) = RunAsRoot("ls", "-ldn", "--", path);
            return exitCode == 0 ? ParseNumericOwnerFromLs(output) : null;
        }

        bool RunChownAsRoot(params string[] arguments)
        {
            var (exitCode, _, error) = RunAsRoot("chown", arguments);
            if (exitCode is not null and not 0)
                Log.Verbose($"chown {string.Join(" ", arguments)} exited with {exitCode}: {error}");
            return exitCode == 0;
        }

        /// <summary>
        /// Runs <paramref name="program"/> as root and returns its exit code and output. Only root can give a file away,
        /// so if we are not root nothing is run. The exit code is null whenever the program did not run to completion: not
        /// root, it could not be started, it timed out, or it threw.
        /// </summary>
        (int? ExitCode, string Output, string Error) RunAsRoot(string program, params string[] arguments)
        {
            if (OperatingSystem.IsWindows() || Environment.UserName != "root")
                return (null, "", "");

            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo(program)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var argument in arguments)
                    startInfo.ArgumentList.Add(argument);

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null)
                    return (null, "", "");
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(10_000))
                {
                    process.Kill();
                    return (null, "", "");
                }

                return (process.ExitCode, output.GetAwaiter().GetResult(), error);
            }
            catch (Exception e)
            {
                Log.Verbose(e, $"Unable to run {program} {string.Join(" ", arguments)}");
                return (null, "", "");
            }
        }
#endif

        /// <summary>
        /// Parses the uid and gid out of one line of <c>ls -ln</c> output (<c>-rw-r--r-- 1 1000 1000 42 Jan 1 00:00 file</c>)
        /// as <c>uid:gid</c>, or null if the line is not in that shape.
        /// </summary>
        public static string? ParseNumericOwnerFromLs(string lsOutput)
        {
            var fields = (lsOutput ?? "").Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 5 || !uint.TryParse(fields[2], out var uid) || !uint.TryParse(fields[3], out var gid))
                return null;
            return $"{uid}:{gid}";
        }

        // In the same directory, so it is on the same file system and can be moved into place atomically.
        static string TemporaryPathBeside(string path)
            => path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        public void EnsureDirectoryExists(string directoryPath)
        {
            if (!DirectoryExists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        } // ReSharper disable AssignNullToNotNullAttribute

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath)
        {
            EnsureDiskHasEnoughFreeSpace(directoryPath, FiveHundredMegabytes);
        }

        public virtual void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            if (IsUncPath(directoryPath))
                return;

            if (!Path.IsPathRooted(directoryPath))
                return;

            //We can't perform this check in Kubernetes due to how drives are mounted and reported (always returns 0 byte sized drives)
            if (IsRunningAsKubernetesAgent)
                return;

            var driveInfo = SafelyGetDriveInfo(directoryPath);

            var required = requiredSpaceInBytes < 0 ? 0 : (ulong)requiredSpaceInBytes;
            // Make sure there is 10% (and a bit extra) more than we need
            required += required / 10 + 1024 * 1024;
            if ((ulong)driveInfo.AvailableFreeSpace < required)
                throw new IOException($"The drive '{driveInfo.Name}' containing the directory '{directoryPath}' on machine '{Environment.MachineName}' does not have enough free disk space available for this operation to proceed. " +
                    $"The disk only has {driveInfo.AvailableFreeSpace.ToFileSizeString()} available; please free up at least {required.ToFileSizeString()}.");
        }

        /// <remarks>
        /// Previously, we used to get the directory root (ie, `c:\` or `/`) before asking for the drive info
        /// However, that doesn't work well with mount points, as there might be enough space in that mount point,
        /// but not enough on the root of the drive.
        /// New behaviour is to directly check the free disk space on that directory, but we're feeling a bit
        /// risk averse here (once bitten, twice shy), so we fall back to the old behaviour
        /// </remarks>
        static DriveInfo SafelyGetDriveInfo(string directoryPath)
        {
            DriveInfo driveInfo;
            try
            {
                driveInfo = new DriveInfo(directoryPath);
            }
            catch
            {
                driveInfo = new DriveInfo(Directory.GetDirectoryRoot(directoryPath));
            }

            return driveInfo;
        }

        public string GetFullPath(string relativeOrAbsoluteFilePath)
        {
            try
            {
                if (!Path.IsPathRooted(relativeOrAbsoluteFilePath))
                    relativeOrAbsoluteFilePath = Path.Combine(Environment.CurrentDirectory, relativeOrAbsoluteFilePath);

                relativeOrAbsoluteFilePath = Path.GetFullPath(relativeOrAbsoluteFilePath);
                return relativeOrAbsoluteFilePath;
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"Error processing path {relativeOrAbsoluteFilePath}. If the path was quoted check you are not accidentally escaping the closing quote with a \\ character. Otherwise ensure the path does not contain any illegal characters.", e);
            }
        }

        public string ReadAllText(string scriptFile)
            => File.ReadAllText(scriptFile);

        public string[] ReadAllLines(string scriptFile)
            => File.ReadAllLines(scriptFile);

        static bool IsUncPath(string directoryPath)
            => Uri.TryCreate(directoryPath, UriKind.Absolute, out var uri) && uri.IsUnc;

        async Task TryToDoSomethingMultipleTimes(
            Action<int> thingToDo,
            int numberAttempts,
            int sleepTime,
            bool throwOnFailure,
            CancellationToken cancellationToken)
        {
            if (numberAttempts < 1)
            {
                Log.Error("Trying to do something less than once, doesn't make much sense");
                return;
            }

            for (var i = 1; i <= numberAttempts; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Run(() => thingToDo(i), cancellationToken);
                    break;
                }
                catch (Exception e)
                {
                    Thread.Sleep(sleepTime);
                    if (i == numberAttempts)
                    {
                        if (throwOnFailure)
                        {
                            Log.Error(e, $"Failed to complete action, attempted {numberAttempts} time(s), throwing error");
                            throw;
                        }

                        Log.Error(e, $"Failed to complete action, attempted {numberAttempts} time(s), silently moving on...");
                        break;
                    }
                }
            }
        }
    }
}