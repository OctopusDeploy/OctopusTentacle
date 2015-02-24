using System;
using System.IO;

namespace Octopus.Shared.Artifacts
{
    public interface IArtifactStore
    {
        void Save(string path, Stream data, string serverTaskId);
    }
}
