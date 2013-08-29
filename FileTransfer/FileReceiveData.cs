using System;

namespace Octopus.Shared.FileTransfer
{
    public class FileReceiveData
    {
        public string LocalPath { get; set; }
        public string Hash { get; set; }
    }
}