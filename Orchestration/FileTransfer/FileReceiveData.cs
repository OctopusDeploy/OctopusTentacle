using System;

namespace Octopus.Shared.Orchestration.FileTransfer.Implementation
{
    public class FileReceiveData
    {
        public string LocalPath { get; set; }
        public string Hash { get; set; }
    }
}