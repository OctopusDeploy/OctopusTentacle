using System;
using Octopus.Diagnostics;

namespace Octopus.Shared.Diagnostics
{
    public class SystemLog : Log, ISystemLog
    {
        protected override string CorrelationId => "system/" + Environment.MachineName;
    }
}