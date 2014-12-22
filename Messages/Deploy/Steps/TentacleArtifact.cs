using System;

namespace Octopus.Shared.Messages.Deploy.Steps
{
    public class TentacleArtifact
    {
        public string Path { get; private set; }
        public string OriginalFilename { get; private set; }

        public TentacleArtifact(string path, string originalFilename)
        {
            Path = path;
            OriginalFilename = originalFilename;
        }
    }
}