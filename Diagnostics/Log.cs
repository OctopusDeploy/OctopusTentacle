using System;

namespace Octopus.Shared.Diagnostics
{
    public static class Log
    {
        static Func<ILog> logFactory;

        public static ILog Octopus()
        {
            var factory = logFactory;
            return factory == null ? new NullLog() : factory();
        }

        public static void SetFactory(Func<ILog> newLogFactory)
        {
            logFactory = newLogFactory;
        }
    }
}