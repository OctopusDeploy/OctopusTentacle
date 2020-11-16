using System;
using System.IO;

namespace Octopus.Shared.Util
{
    public class FileInfoAdapter : IFileInfo
    {
        readonly FileInfo info;

        public FileInfoAdapter(FileInfo info)
        {
            this.info = info;
        }

        public string FullPath => info.FullName;

        public string Extension => info.Extension;

        public DateTime LastAccessTimeUtc => info.LastAccessTimeUtc;

        public DateTime LastWriteTimeUtc => info.LastWriteTimeUtc;

        public long Length => info.Length;
    }
}