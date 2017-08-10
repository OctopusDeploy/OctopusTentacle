using System;

namespace Octopus.Shared.Contracts
{
    public class UploadResult
    {
        readonly string fullPath;
        readonly string hash;
        readonly long length;

        public UploadResult(string fullPath, string hash, long length)
        {
            this.fullPath = fullPath;
            this.hash = hash;
            this.length = length;
        }

        public string FullPath
        {
            get { return fullPath; }
        }

        public string Hash
        {
            get { return hash; }
        }

        public long Length
        {
            get { return length; }
        }
    }
}