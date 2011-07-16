using System;
using System.Text;
using log4net;

namespace Octopus.Shared.Diagnostics
{
    /// <summary>
    /// Like a wire tap, intercepts any log4net log messages written by the current thread within the scope
    /// of this using block, and appends them to a given string builder.
    /// </summary>
    public class LogTapper : IDisposable
    {
        LogTapper(StringBuilder builder)
        {
            ThreadContext.Properties["LogOutputTo"] = builder;
        }

        public static IDisposable CaptureTo(StringBuilder builder)
        {
            return new LogTapper(builder);
        }

        public void Dispose()
        {
            ThreadContext.Properties.Remove("LogOutputTo");
        }
    }
}