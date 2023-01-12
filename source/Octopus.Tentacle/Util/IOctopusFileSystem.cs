using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Octopus.Tentacle.Util
{
    public interface IOctopusFileSystem
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        void DeleteFile(string path, DeletionOptions? options = null);
        void DeleteDirectory(string path, DeletionOptions? options = null);
        IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns);
        long GetFileSize(string path);
        string ReadFile(string path);
        void OverwriteFile(string path, string contents);
        void OverwriteFile(string path, string contents, Encoding encoding);
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
        string ReadAllText(string scriptFile);
        string[] ReadAllLines(string scriptFile);
    }
}
