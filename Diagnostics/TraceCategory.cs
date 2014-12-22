using System;

namespace Octopus.Shared.Diagnostics
{
    public enum TraceCategory
    {
        Trace = 1,
        Verbose = 100,
        Info = 200,
        Warning = 300,
        Error = 400,
        Fatal = 500
    }
}
