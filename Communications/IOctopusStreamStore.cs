using System;
using Pipefish.Streaming;

namespace Octopus.Shared.Communications
{
    public interface IOctopusStreamStore : IStreamStore
    {
        void Move(StreamIdentifier streamIdentifier, string newFilePath);
    }
}