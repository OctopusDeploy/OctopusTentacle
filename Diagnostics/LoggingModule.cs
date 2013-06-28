using System;
using Autofac;

namespace Octopus.Shared.Diagnostics
{
    public class LoggingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => Log.Octopus()).As<ILog>();
            builder.RegisterType<ErrorReporter>().As<IErrorReporter>();
        }
    }
}