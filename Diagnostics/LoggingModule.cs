using System;
using Autofac;
using Octopus.Platform.Diagnostics;

namespace Octopus.Shared.Diagnostics
{
    public class LoggingModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var logAdapter = new LogAdapter();
            Log.SetFactory(() => logAdapter);
            builder.Register(c => Log.Octopus()).As<ILog>();
        }
    }
}