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

        void WriteAllTextOwnerOnly(string filePath, string contents);

        // Written under a temporary name and moved into place without replacing anything. Returns false, leaving the
        // existing file untouched, if another process created it first.
        bool TryCreateFileOwnerOnly(string filePath, string contents);

        bool TryCreateFileOwnerOnly(string filePath, byte[] contents);

        // Only root can give a file away: these return false without trying when not root on Unix.
        bool TryChangeOwnerToMatch(string filePath, string referencePath);

        // reason says why when this returns false, in words fit for a Warn.
        bool TryChangeOwner(string filePath, string userName, out string reason);

        bool RestrictFilePermissionsToOwner(string filePath);
        string ReadAllText(string scriptFile);
        string[] ReadAllLines(string scriptFile);
    }
}
