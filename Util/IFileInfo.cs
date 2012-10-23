using System;

namespace Octopus.Shared.Util
{
    public interface IFileInfo
    {
        string Extension { get; }
        DateTime LastAccessTimeUtc { get; }
        DateTime LastWriteTimeUtc { get; }
    }
}