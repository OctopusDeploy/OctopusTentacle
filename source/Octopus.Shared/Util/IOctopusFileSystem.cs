using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Shared.Util
{
    public interface IOctopusFileSystem
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        bool DirectoryIsEmpty(string path);
        void DeleteFile(string path);
        void DeleteFile(string path, DeletionOptions options);
        void DeleteDirectory(string path);
        void DeleteDirectory(string path, DeletionOptions options);
        Task DeleteDirectory(string path, CancellationToken cancellationToken);
        string GetCurrentDirectory();
        IEnumerable<string> EnumerateDirectories(string parentDirectoryPath);
        IEnumerable<string> EnumerateDirectoriesRecursively(string parentDirectoryPath);
        IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns);
        IEnumerable<string> EnumerateFiles<TKey>(string parentDirectoryPath, Func<IFileInfo, TKey> order, params string[] searchPatterns);
        IEnumerable<string> EnumerateFilesRecursively(string parentDirectoryPath, params string[] searchPatterns);
        long GetFileSize(string path);
        DateTimeOffset GetFileLastWriteTimeUtc(string path);
        DateTimeOffset GetFileCreationTimeUtc(string path);
        string ReadFile(string path);
        void AppendToFile(string path, string contents);
        void OverwriteFile(string path, string contents);
        void OverwriteFile(string path, string contents, Encoding encoding);
        Stream OpenFile(string path, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        Stream OpenFile(string path, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        Stream CreateTemporaryFile(string filename, out string path);
        void CreateDirectory(string path);
        string CreateTemporaryDirectory();
        void CopyDirectory(string sourceDirectory, string targetDirectory, int overwriteFileRetryAttempts = 3);
        void CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancel, int overwriteFileRetryAttempts = 3);
        ReplaceStatus CopyFile(string sourceFile, string destinationFile, int overwriteFileRetryAttempts = 3);
        void PurgeDirectory(string targetDirectory, DeletionOptions options);
        void PurgeDirectory(string targetDirectory, DeletionOptions options, CancellationToken cancel);
        void PurgeDirectory(string targetDirectory, Predicate<IFileInfo> filter, DeletionOptions options);
        void PurgeDirectory(string targetDirectory, Predicate<IFileInfo> filter, DeletionOptions options, Func<string, IEnumerable<string>> fileEnumerator);
        void EnsureDirectoryExists(string directoryPath);
        void EnsureDiskHasEnoughFreeSpace(string directoryPath);
        void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes);
        bool DiskHasEnoughFreeSpace(string directoryPath);
        bool DiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes);
        
        /// <summary>
        /// Resolves the full file path. Relative paths are taken relative to current working directory
        /// </summary>
        /// <param name="relativeOrAbsoluteFilePath"></param>
        /// <returns></returns>
        string GetFullPath(string relativeOrAbsoluteFilePath);
        void OverwriteAndDelete(string originalFile, string temporaryReplacement);
        void WriteAllBytes(string filePath, byte[] data);
        void WriteAllText(string filePath, string contents);
        string RemoveInvalidFileNameChars(string path);
        void MoveFile(string sourceFile, string destinationFile);
        void MoveDirectory(string sourceDirectory, string destinationDirectory);
        ReplaceStatus Replace(string path, Stream stream, int overwriteRetryAttempts = 3);
        bool EqualHash(Stream first, Stream second);
        string ReadAllText(string scriptFile);
        string[] ReadAllLines(string scriptFile);
        string GetFileVersion(string file);
    }
}
