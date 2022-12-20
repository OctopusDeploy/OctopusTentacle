using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Polly;

namespace Octopus.Tentacle.Util
{
    public class OctopusPhysicalFileSystem : IOctopusFileSystem
    {
        const long FiveHundredMegabytes = 500 * 1024 * 1024;

        public OctopusPhysicalFileSystem(ISystemLog log)
        {
            Log = log;
        }

        ISystemLog Log { get; }

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

        public string ReadFile(string path)
        {
            var content = Policy<string>
                .Handle<IOException>()
                .WaitAndRetry(10, retryCount => TimeSpan.FromMilliseconds(100 * retryCount))
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

        public void EnsureDirectoryExists(string directoryPath)
        {
            if (!DirectoryExists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        } // ReSharper disable AssignNullToNotNullAttribute

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath)
        {
            EnsureDiskHasEnoughFreeSpace(directoryPath, FiveHundredMegabytes);
        }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            if (IsUncPath(directoryPath))
                return;

            if (!Path.IsPathRooted(directoryPath))
                return;
            
            var driveInfo = SafelyDriveInfo(directoryPath);

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
        static DriveInfo SafelyDriveInfo(string directoryPath)
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
