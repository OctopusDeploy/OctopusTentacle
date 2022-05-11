using System;

namespace Octopus.Shared.Configuration
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