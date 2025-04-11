using System;

namespace Octopus.Tentacle.Core.Diagnostics
{
    public interface ISystemLog : ILog, IDisposable
    {
        ISystemLog ChildContext(string[] sensitiveValues);
    }
}