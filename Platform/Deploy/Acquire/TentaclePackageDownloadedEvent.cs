using System;
using Pipefish;

namespace Octopus.Shared.Platform.Deploy.Acquire
{
    public class TentaclePackageDownloadedEvent : IMessage
    {
        public string Hash { get; set; }
        public long Size { get; set; }

        public TentaclePackageDownloadedEvent(string hash, long size)
        {
            Hash = hash;
            Size = size;
        }
    }
}