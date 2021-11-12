using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Polly;

namespace Octopus.Shared.Util
{
    public class OctopusPhysicalFileSystem : IOctopusFileSystem
    {
        // https://referencesource.microsoft.com/#mscorlib/system/io/pathinternal.cs,30
        // This even applies to long file names https://stackoverflow.com/a/265782/10784
        public const int MaxComponentLength = 255;

        const long FiveHundredMegabytes = 500 * 1024 * 1024;

        static readonly char[] InvalidFileNameChars =
        {
            // From Path.InvalidPathChars which covers Windows and Linux
            '"',
            '<',
            '>',
            '|',
            char.MinValue,
            '\x0001',
            '\x0002',
            '\x0003',
            '\x0004',
            '\x0005',
            '\x0006',
            '\a',
            '\b',
            '\t',
            '\n',
            '\v',
            '\f',
            '\r',
            '\x000E',
            '\x000F',
            '\x0010',
            '\x0011',
            '\x0012',
            '\x0013',
            '\x0014',
            '\x0015',
            '\x0016',
            '\x0017',
            '\x0018',
            '\x0019',
            '\x001A',
            '\x001B',
            '\x001C',
            '\x001D',
            '\x001E',
            '\x001F',
            ':',
            '*',
            '?',
            '\\',
            '/'
        };

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

        public string GetCurrentDirectory() => Directory.GetCurrentDirectory();

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
                includeFilter: null,
                cancellationToken,
                includeTarget: true,
                fileEnumerationFunc: null,
                options);
        }

        public IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns)
        {
            return searchPatterns.Length == 0
                ? Directory.EnumerateFiles(parentDirectoryPath, "*", SearchOption.TopDirectoryOnly)
                : searchPatterns.SelectMany(pattern => Directory.EnumerateFiles(parentDirectoryPath, pattern, SearchOption.TopDirectoryOnly));
        }

        public IEnumerable<string> EnumerateFiles<TKey>(string parentDirectoryPath, Func<IFileInfo, TKey> order, params string[] searchPatterns)
        {
            var files = EnumerateFiles(parentDirectoryPath, searchPatterns).Select(f => new FileInfoAdapter(new FileInfo(f)));
            return files.OrderBy(order).Select(f => f.FullPath);
        }

        public IEnumerable<string> EnumerateFiles<TKey>(string parentDirectoryPath, Func<FileInfo, TKey> order, params string[] searchPatterns)
        {
            var files = EnumerateFiles(parentDirectoryPath, searchPatterns);
            var fileInfos = files.Select(f => new FileInfo(f)).OrderBy(order);
            return fileInfos.Select(x => x.FullName);
        }

        public IEnumerable<string> EnumerateFilesRecursively(string parentDirectoryPath, params string[] searchPatterns)
        {
            if (!DirectoryExists(parentDirectoryPath))
                return Enumerable.Empty<string>();

            return searchPatterns.Length == 0
                ? Directory.EnumerateFiles(parentDirectoryPath, "*", SearchOption.AllDirectories)
                : searchPatterns.Distinct().SelectMany(pattern => Directory.EnumerateFiles(parentDirectoryPath, pattern, SearchOption.AllDirectories));
        }

        public IEnumerable<string> EnumerateDirectories(string parentDirectoryPath)
        {
            if (!DirectoryExists(parentDirectoryPath))
                return Enumerable.Empty<string>();

            return Directory.EnumerateDirectories(parentDirectoryPath);
        }

        public IEnumerable<string> EnumerateDirectoriesRecursively(string parentDirectoryPath)
        {
            if (!DirectoryExists(parentDirectoryPath))
                return Enumerable.Empty<string>();

            return Directory.EnumerateDirectories(parentDirectoryPath, "*", SearchOption.AllDirectories);
        }

        public long GetFileSize(string path)
            => new FileInfo(path).Length;

        public DateTimeOffset GetFileLastWriteTimeUtc(string path)
            => File.GetLastWriteTimeUtc(path);

        public DateTimeOffset GetFileCreationTimeUtc(string path)
            => File.GetCreationTimeUtc(path);

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

        public Stream CreateTemporaryFile(string filename, out string path)
        {
            path = Path.Combine(GetTempBasePath(), filename);
            var dir = Path.GetDirectoryName(path) ?? throw new ArgumentException("Directory required");
            EnsureDirectoryExists(dir);

            DeleteFile(path);
            return OpenFile(path, FileAccess.ReadWrite, FileShare.Read);
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

        public void PurgeDirectory(string targetDirectory, DeletionOptions options)
        {
            PurgeDirectoryAsync(
                    targetDirectory,
                    includeFilter: null,
                    cancel: null,
                    includeTarget: null,
                    fileEnumerationFunc: null,
                    options)
                .Wait();
        }

        public Task PurgeDirectory(string targetDirectory, CancellationToken cancel, DeletionOptions? options = null)
        {
            return PurgeDirectoryAsync(
                targetDirectory,
                includeFilter: null,
                cancel,
                includeTarget: null,
                fileEnumerationFunc: null,
                options);
        }

        public void PurgeDirectory(string targetDirectory, DeletionOptions options, CancellationToken cancel)
        {
            PurgeDirectoryAsync(
                    targetDirectory,
                    includeFilter: null,
                    cancel,
                    includeTarget: null,
                    fileEnumerationFunc: null,
                    options
                )
                .Wait(cancel);
        }

        public void PurgeDirectory(string targetDirectory, Predicate<IFileInfo> filter, DeletionOptions options)
        {
            PurgeDirectoryAsync(
                    targetDirectory,
                    filter,
                    cancel: null,
                    includeTarget: null,
                    fileEnumerationFunc: null,
                    options)
                .Wait();
        }

        public Task PurgeDirectory(string targetDirectory, Predicate<IFileInfo> filter, CancellationToken cancel, DeletionOptions? options = null)
        {
            return PurgeDirectoryAsync(
                targetDirectory,
                filter,
                cancel,
                includeTarget: null,
                fileEnumerationFunc: null,
                options);
        }

        public Task PurgeDirectory(string targetDirectory,
            Predicate<IFileInfo> filter,
            Func<string, IEnumerable<string>> fileEnumerator,
            CancellationToken cancel,
            DeletionOptions? options = null)
        {
            return PurgeDirectoryAsync(
                targetDirectory,
                filter,
                cancel,
                includeTarget: null,
                fileEnumerationFunc: null,
                options);
        }

        public void PurgeDirectory(string targetDirectory, Predicate<IFileInfo> include, DeletionOptions options, Func<string, IEnumerable<string>> fileEnumerator)
        {
            PurgeDirectoryAsync(targetDirectory,
                    include,
                    cancel: null,
                    includeTarget: null,
                    fileEnumerationFunc: fileEnumerator,
                    options)
                .Wait();
        }

        static readonly CancellationToken DefaultCancellationToken = CancellationToken.None;

        IEnumerable<string> DefaultFileEnumerationFunc(string target)
        {
            return EnumerateFiles(target);
        }

        async Task PurgeDirectoryAsync(
            string targetDirectory,
            Predicate<IFileInfo>? includeFilter,
            CancellationToken? cancel,
            bool? includeTarget,
            Func<string, IEnumerable<string>>? fileEnumerationFunc,
            DeletionOptions? options)
        {
            if (!DirectoryExists(targetDirectory))
                return;

            includeFilter ??= fileInfo => true;
            cancel ??= CancellationToken.None;
            includeTarget ??= false;
            options ??= DeletionOptions.TryThreeTimes;
            var fileEnumerator = fileEnumerationFunc ?? DefaultFileEnumerationFunc;

            foreach (var file in fileEnumerator(targetDirectory))
            {
                if (includeFilter != null)
                {
                    var info = new FileInfoAdapter(new FileInfo(file));
                    if (!includeFilter(info))
                        continue;
                }

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
                        includeFilter,
                        cancel,
                        true,
                        fileEnumerator,
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

        public void OverwriteAndDelete(string originalFile, string temporaryReplacement)
        {
            var backup = originalFile + ".backup" + Guid.NewGuid();

            if (!File.Exists(originalFile))
                File.Copy(temporaryReplacement, originalFile, true);
            else
                File.Replace(temporaryReplacement, originalFile, backup);

            File.Delete(temporaryReplacement);
            if (File.Exists(backup))
                File.Delete(backup);
        }

        public void WriteAllBytes(string filePath, byte[] data)
        {
            File.WriteAllBytes(filePath, data);
        }

        public void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        public string RemoveInvalidFileNameChars(string path)
        {
            path = new string(path.Where(c => !InvalidFileNameChars.Contains(c)).ToArray());
            return path;
        }

        public void MoveFile(string sourceFile, string destinationFile)
        {
            File.Move(sourceFile, destinationFile);
        }

        public void MoveDirectory(string sourceDirectory, string destinationDirectory)
        {
            new DirectoryInfo(sourceDirectory).MoveTo(destinationDirectory);
        }

        public void EnsureDirectoryExists(string directoryPath)
        {
            if (!DirectoryExists(directoryPath))
                Directory.CreateDirectory(directoryPath);
        } // ReSharper disable AssignNullToNotNullAttribute

        public void CopyDirectory(string sourceDirectory, string targetDirectory, int overwriteFileRetryAttempts = 3)
        {
            CopyDirectory(sourceDirectory, targetDirectory, CancellationToken.None, overwriteFileRetryAttempts);
        }

        public void CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancel, int overwriteFileRetryAttempts = 3)
        {
            if (!DirectoryExists(sourceDirectory))
                return;

            if (!DirectoryExists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            var files = Directory.GetFiles(sourceDirectory, "*");
            foreach (var sourceFile in files)
            {
                cancel.ThrowIfCancellationRequested();

                var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
                CopyFile(sourceFile, targetFile, overwriteFileRetryAttempts);
            }

            foreach (var childSourceDirectory in Directory.GetDirectories(sourceDirectory))
            {
                var name = Path.GetFileName(childSourceDirectory);
                var childTargetDirectory = Path.Combine(targetDirectory, name);
                CopyDirectory(childSourceDirectory, childTargetDirectory, cancel, overwriteFileRetryAttempts);
            }
        }

        public ReplaceStatus CopyFile(string sourceFile, string targetFile, int overwriteFileRetryAttempts = 3)
        {
            var result = ReplaceStatus.Updated;
            for (var i = 0; i < overwriteFileRetryAttempts; i++)
                try
                {
                    FileInfo fi = new FileInfo(targetFile);
                    if (fi.Exists)
                    {
                        if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            fi.Attributes = fi.Attributes & ~FileAttributes.ReadOnly;
                    }
                    else
                    {
                        result = ReplaceStatus.Created;
                    }

                    File.Copy(sourceFile, targetFile, true);
                    return result;
                }
                catch
                {
                    if (i == overwriteFileRetryAttempts - 1)
                        throw;
                    Thread.Sleep(1000 + 2000 * i);
                }

            throw new Exception("Internal error, cannot get here");
        }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath)
        {
            EnsureDiskHasEnoughFreeSpace(directoryPath, FiveHundredMegabytes);
        }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            if (IsUncPath(directoryPath))
                return;

            var driveInfo = new DriveInfo(Directory.GetDirectoryRoot(directoryPath));

            var required = requiredSpaceInBytes < 0 ? 0 : (ulong)requiredSpaceInBytes;
            // Make sure there is 10% (and a bit extra) more than we need
            required += required / 10 + 1024 * 1024;
            if ((ulong)driveInfo.AvailableFreeSpace < required)
                throw new IOException($"The drive containing the directory '{directoryPath}' on machine '{Environment.MachineName}' does not have enough free disk space available for this operation to proceed. The disk only has {driveInfo.AvailableFreeSpace.ToFileSizeString()} available; please free up at least {required.ToFileSizeString()}.");
        }

        public bool DiskHasEnoughFreeSpace(string directoryPath)
            => DiskHasEnoughFreeSpace(directoryPath, FiveHundredMegabytes);

        public bool DiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            if (IsUncPath(directoryPath))
                return true;

            var driveInfo = new DriveInfo(Directory.GetDirectoryRoot(directoryPath));
            return driveInfo.AvailableFreeSpace > requiredSpaceInBytes;
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

        /// <summary>
        /// Creates, updates or skips a file based on a file content comparison
        /// </summary>
        /// <remarks>
        /// Useful for cases where you do not want a file's timestamp to change when overwriting
        /// it with identical contents or you want clearer logging as to what changed.
        /// </remarks>
        public ReplaceStatus Replace(string oldFilePath, Stream newStream, int overwriteFileRetryAttempts = 3)
        {
            for (var i = 0; i < overwriteFileRetryAttempts; i++)
                try
                {
                    var oldDirectory = Path.GetDirectoryName(oldFilePath) ?? throw new ArgumentException("directoryRequired", nameof(oldFilePath));
                    if (!DirectoryExists(oldDirectory))
                        Directory.CreateDirectory(oldDirectory);

                    var fi = new FileInfo(oldFilePath);
                    if (!fi.Exists)
                    {
                        // Getting unauthorized exception - file is readonly
                        using (var fileStream = File.Create(oldFilePath))
                        {
                            newStream.CopyTo(fileStream);
                            fileStream.Flush();
                        }

                        return ReplaceStatus.Created;
                    }

                    if ((fi.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                        // Throw a more helpful message than .NET's
                        // System.UnauthorizedAccessException: Access to the path ... is denied.
                        throw new IOException("Cannot overwrite a directory with a file " + oldFilePath);

                    if ((fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        fi.Attributes = fi.Attributes & ~FileAttributes.ReadOnly;

                    bool equal;
                    using (var oldStream = File.OpenRead(oldFilePath))
                    {
                        equal = EqualHash(oldStream, newStream);
                    }

                    if (equal)
                        return ReplaceStatus.Unchanged;
                    newStream.Seek(0, SeekOrigin.Begin);
                    using (var oldStream = File.Create(oldFilePath))
                    {
                        newStream.CopyTo(oldStream);
                        newStream.Flush();
                    }

                    return ReplaceStatus.Updated;
                }
                catch
                {
                    if (i == overwriteFileRetryAttempts - 1)
                        throw;
                    Thread.Sleep(1000 + 2000 * i);
                }

            throw new Exception("Internal error, cannot get here");
        }

        public string ReadAllText(string scriptFile)
            => File.ReadAllText(scriptFile);

        public string[] ReadAllLines(string scriptFile)
            => File.ReadAllLines(scriptFile);

        public string GetFileVersion(string file)
            => FileVersionInfo.GetVersionInfo(file).FileVersion;

        public bool EqualHash(Stream first, Stream second)
        {
            if (first == null || second == null)
                return false;

            first.Seek(0, SeekOrigin.Begin);
            second.Seek(0, SeekOrigin.Begin);

            using (var cryptoProvider = new SHA1CryptoServiceProvider())
            {
                var firstHash = cryptoProvider.ComputeHash(first);
                var secondHash = cryptoProvider.ComputeHash(second);

                for (var i = 0; i < firstHash.Length; i++)
                    if (firstHash[i] != secondHash[i])
                        return false;
                return true;
            }
        }

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
