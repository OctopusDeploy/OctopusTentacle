using System;
using Autofac;

namespace Octopus.Shared.Time
{
    public class TimeModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<SystemClock>().As<IClock>().SingleInstance();
            builder.RegisterType<Sleep>().As<ISleep>().SingleInstance();
        }
    }
}