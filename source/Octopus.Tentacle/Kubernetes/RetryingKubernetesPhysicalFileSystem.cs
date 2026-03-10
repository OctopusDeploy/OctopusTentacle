using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Util;
using Polly;
using Polly.Retry;

namespace Octopus.Tentacle.Kubernetes
{
    public class RetryingKubernetesPhysicalFileSystem : IOctopusFileSystem
    {
        readonly IOctopusFileSystem inner;
        readonly AsyncRetryPolicy asyncRetryPolicy;
        readonly RetryPolicy syncRetryPolicy;

        public RetryingKubernetesPhysicalFileSystem(IOctopusFileSystem inner)
        {
            this.inner = inner;

            // There are scenarios where we the IO can have errors, mainly due to the NFS being restarted.
            // This retry policy retries up to 5 times, with an exponential backoff from a base of 50ms. The last retry will be ~1.5s.
            Func<int, TimeSpan> sleepDurationFunc = retry => TimeSpan.FromMilliseconds(50 * Math.Pow(retry, 2));
            asyncRetryPolicy = Policy.Handle<IOException>()
                .WaitAndRetryAsync(5, sleepDurationFunc);
            syncRetryPolicy = Policy.Handle<IOException>()
                .WaitAndRetry(5, sleepDurationFunc);
        }

        public bool FileExists(string path)
        {
            return syncRetryPolicy.Execute(() => inner.FileExists(path));
        }

        public bool DirectoryExists(string path)
        {
            return syncRetryPolicy.Execute(() => inner.DirectoryExists(path));
        }

        public void DeleteFile(string path, DeletionOptions? options = null)
        {
            syncRetryPolicy.Execute(() => inner.DeleteFile(path, options));
        }

        public void DeleteDirectory(string path, DeletionOptions? options = null)
        {
            syncRetryPolicy.Execute(() => inner.DeleteDirectory(path, options));
        }

        public async Task DeleteDirectory(string path, CancellationToken cancellationToken, DeletionOptions? options = null)
        {
            await asyncRetryPolicy.ExecuteAsync(async ct => await inner.DeleteDirectory(path, ct, options), cancellationToken);
        }

        public IEnumerable<string> EnumerateFiles(string parentDirectoryPath, params string[] searchPatterns)
        {
            return syncRetryPolicy.Execute(() => inner.EnumerateFiles(parentDirectoryPath, searchPatterns));
        }

        public long GetFileSize(string path)
        {
            return syncRetryPolicy.Execute(() => inner.GetFileSize(path));
        }

        public string ReadFile(string path, bool withRetry = true)
        {
            return !withRetry
                ? inner.ReadFile(path, withRetry)
                //we want to handle the retry at this level
                : syncRetryPolicy.Execute(() => inner.ReadFile(path, false));
        }

        public void OverwriteFile(string path, string contents)
        {
            syncRetryPolicy.Execute(() => inner.OverwriteFile(path, contents));
        }

        public void OverwriteFile(string path, string contents, Encoding encoding)
        {
            syncRetryPolicy.Execute(() => inner.OverwriteFile(path, contents, encoding));
        }

        public void CopyFile(string source, string destination, bool overwrite)
        {
            syncRetryPolicy.Execute(() => inner.CopyFile(source, destination, overwrite));
        }

        public Stream OpenFile(string path, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read)
        {
            return syncRetryPolicy.Execute(() => inner.OpenFile(path, access, share));
        }

        public Stream OpenFile(string path, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.Read)
        {
            return syncRetryPolicy.Execute(() => inner.OpenFile(path, mode, access, share));
        }

        public void CreateDirectory(string path)
        {
            syncRetryPolicy.Execute(() => inner.CreateDirectory(path));
        }

        public void EnsureDirectoryExists(string directoryPath)
        {
            syncRetryPolicy.Execute(() => inner.EnsureDirectoryExists(directoryPath));
        }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath)
        {
            syncRetryPolicy.Execute(() => inner.EnsureDiskHasEnoughFreeSpace(directoryPath));
        }

        public void EnsureDiskHasEnoughFreeSpace(string directoryPath, long requiredSpaceInBytes)
        {
            syncRetryPolicy.Execute(() => inner.EnsureDiskHasEnoughFreeSpace(directoryPath, requiredSpaceInBytes));
        }

        public string GetFullPath(string relativeOrAbsoluteFilePath)
        {
            return syncRetryPolicy.Execute(() => inner.GetFullPath(relativeOrAbsoluteFilePath));
        }

        public void WriteAllBytes(string filePath, byte[] data)
        {
            syncRetryPolicy.Execute(() => inner.WriteAllBytes(filePath, data));
        }

        public void WriteAllText(string filePath, string contents)
        {
            syncRetryPolicy.Execute(() => inner.WriteAllText(filePath, contents));
        }

        public string ReadAllText(string scriptFile)
        {
            return syncRetryPolicy.Execute(() => inner.ReadAllText(scriptFile));
        }

        public string[] ReadAllLines(string scriptFile)
        {
            return syncRetryPolicy.Execute(() => inner.ReadAllLines(scriptFile));
        }
    }
}