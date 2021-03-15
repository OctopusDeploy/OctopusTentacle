#if FULL_FRAMEWORK
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using System;
using System.Linq;
using System.Text;
using Autofac;
using Octopus.Diagnostics;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Diagnostics
{
    public class LoggingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Only add the NLogAppender if it isn't already - otherwise we get twice the logs for the price of one
            if (!Log.Appenders.Any(a => a is NLogAppender))
                Log.Appenders.Add(new NLogAppender());

            builder.RegisterType<SystemLog>().As<ISystemLog>().SingleInstance();
            builder.Register(c => new LogFileOnlyLogger()).As<ILogFileOnlyLogger>().InstancePerLifetimeScope();
        }
    }

    class SqlPrettyPrintHandler : ICustomPrettyPrintHandler<SqlException>
    {
        public bool Handle(StringBuilder sb, Exception ex)
        {
            var number = ((SqlException)ex).Number;
            sb.AppendLine($"SQL Error {number} - {ex.Message}");
            return true;
        }
    }
    class ControlledFailureExceptionPrettyPrintHandler : ICustomPrettyPrintHandler<ControlledFailureException>
    {
        public bool Handle(StringBuilder sb, Exception ex)
        {
            sb.AppendLine(ex.Message);
            return false;
        }
    }

}