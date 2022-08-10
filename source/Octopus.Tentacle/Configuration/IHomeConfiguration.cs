using System;

namespace Octopus.Tentacle.Configuration
{
    public interface IHomeConfiguration
    {
        string? ApplicationSpecificHomeDirectory { get; }
        string? HomeDirectory { get; }
    }

    public interface IWritableHomeConfiguration : IHomeConfiguration
    {
        bool SetHomeDirectory(string? homeDirectory);
    }
}