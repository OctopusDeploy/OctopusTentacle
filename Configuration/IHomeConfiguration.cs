using System;

namespace Octopus.Shared.Configuration
{
    public interface IHomeConfiguration
    {
        string HomeDirectory { get; set; }
    }
}