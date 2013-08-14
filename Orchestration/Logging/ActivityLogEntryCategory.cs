using System;

namespace Octopus.Shared.Orchestration.Logging
{
    // This is a union of TraceCategory and
    // ProgressMessageCategory - it isn't clear yet
    // yet whether these two enumerations should
    // be separate.
    public enum ActivityLogEntryCategory
    {
        Trace,
        Verbose,
        Info,
        Alert,
        Warning,
        Error,
        Fatal,
        Planned,
        Updated,
        Finished,
        Abandoned
    }
}