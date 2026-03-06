using System;
using Octopus.Tentacle.Core.Configuration;

namespace Octopus.Tentacle.Configuration
{
    public interface IHomeConfiguration : IHomeDirectoryProvider
    {
        void WriteTo(IWritableKeyValueStore outputStore);
    }

    public interface IWritableHomeConfiguration : IHomeConfiguration
    {
        bool SetHomeDirectory(string? homeDirectory);
    }
}