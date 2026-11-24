using System;
using System.Collections.Generic;
using Octopus.Tentacle.Core.Configuration;

namespace Octopus.Tentacle.Configuration
{
    public interface IHomeConfiguration : IHomeDirectoryProvider
    {
        IEnumerable<KeyValuePair<string, object?>> DisplaySettings();
    }

    public interface IWritableHomeConfiguration : IHomeConfiguration
    {
        bool SetHomeDirectory(string? homeDirectory);
    }
}