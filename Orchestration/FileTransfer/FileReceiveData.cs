using System;

namespace Octopus.Shared.Orchestration.FileTransfer
{
    public class FileReceiveData
    {
        public string LocalPath { get; set; }
        public string Hash { get; set; }
    }
}