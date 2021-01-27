using System;
using System.Linq;
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

            var log = Log.Octopus();
            builder.Register(c => log).As<ILog>().As<ILogWithContext>().SingleInstance();
            builder.Register(c => new CorrelationId()).InstancePerLifetimeScope();
            builder.Register(c => new LogFileOnlyLogger()).As<ILogFileOnlyLogger>().InstancePerLifetimeScope();
        }
    }
}