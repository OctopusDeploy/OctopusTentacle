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
        readonly ILogScope scope;
        readonly ILogScope parent;

        LogTapper(ILogScope scope)
        {
            this.scope = scope;
            
            parent = ThreadContext.Properties["LogOutputTo"] as ILogScope;
            
            ThreadContext.Properties["LogOutputTo"] = scope;
        }

        public static IDisposable CaptureTo(StringBuilder builder)
        {
            return new LogTapper(new StringBuilderScope(builder));
        }

        public static IDisposable CaptureTo(ILogScope logScope)
        {
            return new LogTapper(logScope);
        }

        public void Dispose()
        {
            scope.Close();
            ThreadContext.Properties.Remove("LogOutputTo");

            if (parent != null)
            {
                ThreadContext.Properties["LogOutputTo"] = parent;
            }
        }

        private class StringBuilderScope : ILogScope
        {
            readonly StringBuilder builder;

            public StringBuilderScope(StringBuilder builder)
            {
                this.builder = builder;
            }

            public void Log(string text)
            {
                builder.AppendLine(text);
            }

            public void Close()
            {
                
            }
        }
    }
}