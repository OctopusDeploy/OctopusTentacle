using System;
using Mindscape.Raygun4Net.Messages;

namespace Octopus.Shared.Diagnostics
{
    public interface IErrorReporter
    {
        void ReportError(Exception exception);
    }
}