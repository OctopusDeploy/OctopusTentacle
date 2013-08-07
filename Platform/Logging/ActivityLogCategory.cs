using System;

namespace Octopus.Shared.Platform.Logging
{
    public enum ActivityLogCategory
    {
        Verbose = 100,
        Info = 200,
        Alert = 250,
        Warning = 300,
        Error = 400,
        Fatal = 500
    }
}