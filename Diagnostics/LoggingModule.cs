using System;
using Autofac;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Diagnostics
{
    public class LoggingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var logAdapter = new LogAdapter();
            Log.SetFactory(() => logAdapter);
            builder.Register(c => Log.Octopus()).As<ILog>();
            builder.RegisterType<ErrorReporter>().As<IErrorReporter>();
        }
    }
}