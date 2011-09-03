using System;
using System.Collections.Generic;
using Autofac;
using log4net;
using log4net.Appender;

namespace Octopus.Shared.Diagnostics
{
    public class LoggingModule : Module
    {
        public LoggingModule()
        {
            Level = "Debug";
        }

        public string Level { get; set; }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(BuildLog).As<ILog>().SingleInstance();
        }

        object BuildLog(IComponentContext componentContext)
        {
            var log = Logger.Default;
            log.SetLevel(Level);

            log.AddAppender(new LogTapAppender());

            foreach (var appender in componentContext.Resolve<IEnumerable<IAppender>>())
            {
                log.AddAppender(appender);
            }

            return log;
        }
    }
}