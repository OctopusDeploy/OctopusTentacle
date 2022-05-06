using System;

namespace Octopus.Shared.Util
{
    public interface IFileInfo
    {
        string FullPath { get; }
        string Extension { get; }
        DateTime LastAccessTimeUtc { get; }
        DateTime LastWriteTimeUtc { get; }
        long Length { get; }
    }
}