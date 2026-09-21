using System;
using System.Collections.Generic;
using System.IO;
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
        /// Creates or replaces a file that only its owner can read or write (mode 0600 on Unix). The permissions are
        /// applied as the file is created, so it is never briefly readable by others. On Windows this is the same as
        /// <see cref="WriteAllText"/>.
        /// </summary>
        void WriteAllTextOwnerOnly(string filePath, string contents);

        /// <summary>
        /// Changes the permissions of an existing file so that only its owner can read or write it (mode 0600 on Unix).
        /// Returns true if the permissions were changed, false if they were already that restrictive. Does nothing and
        /// returns false on Windows. Throws if the permissions cannot be changed, for example on a read-only volume
        /// or when the caller does not own the file.
        /// </summary>
        bool RestrictFilePermissionsToOwner(string filePath);
        string ReadAllText(string scriptFile);
        string[] ReadAllLines(string scriptFile);
    }
}
