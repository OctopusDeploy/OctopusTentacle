using System;

namespace Octopus.Shared.Messages.Deploy.Steps
{
    public class TentacleArtifact
    {
        public TentacleArtifact(string path, string originalFilename)
        {
            Path = path;
            OriginalFilename = originalFilename;
        }

        public string Path { get; }
        public string OriginalFilename { get; }
    }
}