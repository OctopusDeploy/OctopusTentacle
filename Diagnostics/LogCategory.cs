using System;

namespace Octopus.Shared.Diagnostics
{
    public enum LogCategory
    {
        Trace = 1,
        Verbose = 100,
        Info = 200,
        Planned = 201,
        Abandoned = 220,
        Progress = 230,
        Finished = 240,
        Warning = 300,
        Error = 400,
        Fatal = 500
    }
}