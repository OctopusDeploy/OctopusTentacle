using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Util
{
    public interface IOctopusFileSystem
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        void DeleteFile(string path, DeletionOptions? options = null);
        void DeleteDirectory(string path, DeletionOptions? options = null);
        Task DeleteDirectory(string path, CancellationToken cancellationToken, DeletionOptions? options = null);
        IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns);
        long GetFileSize(string path);
        string ReadFile(string path, bool withRetry = true);
        void OverwriteFile(string path, string contents);
        void OverwriteFile(string path, string contents, Encoding encoding);
        void CopyFile(string source, string destination, bool overwrite);
        Stream OpenFile(string path, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        Stream OpenFile(string path, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read);
        void CreateDirectory(string path);
        void EnsureDirectoryExists(string directoryPath);
        void EnsureDiskHasEnoughFreeSpace(string directoryPath);
        void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes);

        /// <summary>
        /// Resolves the full file path. Relative paths are taken relative to current working directory
        /// </summary>
        /// <param name="relativeOrAbsoluteFilePath"></param>
        /// <returns></returns>
        string GetFullPath(string relativeOrAbsoluteFilePath);

        void WriteAllBytes(string filePath, byte[] data);
        void WriteAllText(string filePath, string contents);

        /// <summary>
        /// Creates a file at filePath with contents IF it doesn't exist.
        /// The operation is as atomic as it can be for the FileSystem.
        /// The resulting file has permissions 0600.
        /// Unix only: throws <see cref="PlatformNotSupportedException"/> on Windows.
        /// </summary>
        /// <returns>True if this call created the file, false if it already existed, in which case the existing file is left untouched.</returns>
#if NET
        [UnsupportedOSPlatform("windows")]
#endif
        bool TryAtomicCreateFileWithOnlyUserReadWritePermissions(string filePath, byte[] contents);

        string ReadAllText(string scriptFile);
        string[] ReadAllLines(string scriptFile);
    }
}
