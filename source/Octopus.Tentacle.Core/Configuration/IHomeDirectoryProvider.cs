using System;

namespace Octopus.Tentacle.Core.Configuration
{
    public interface IHomeDirectoryProvider
    {
        string? HomeDirectory { get; }
    }
}