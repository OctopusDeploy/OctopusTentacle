using System;
using Autofac;

namespace Octopus.Shared.Diagnostics
{
    public class LoggingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var log = Log.Octopus();
            Log.Appenders.Add(new NLogAppender());
            builder.Register(c => log).As<ILog>().SingleInstance();
        }
    }
}