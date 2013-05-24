using System;
using Autofac;

namespace Octopus.Shared.Diagnostics
{
    public class LoggingModule : Module
    {
        readonly string logName;

        public LoggingModule(string logName)
        {
            this.logName = logName;
        }

        protected override void Load(ContainerBuilder builder)
        {
            var log = LogAdapter.GetLogger(logName);
            builder.RegisterInstance(log).As<ILog>().SingleInstance();
        }
    }
}