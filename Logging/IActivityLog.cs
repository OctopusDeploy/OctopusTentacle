using System;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Logging
{
    public interface IActivityLog : ILog
    {
        LoggerReference Current { get; }

        IDisposable CreateChild(string messageText);
        IDisposable CreateChildFormat(string messageFormat, params object[] args);
     
        LoggerReference PlanChild(string messageText);
        LoggerReference PlanChildFormat(string messageFormat, params object[] args);
        
        IDisposable SwitchTo(LoggerReference logger);

        void Abandon();

        void Reinstate();

        void Finish();
    }
}