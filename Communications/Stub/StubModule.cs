using System;
using Autofac;

namespace Octopus.Shared.Communications.Stub
{
    class StubModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ActorStorage>().AsImplementedInterfaces();
            builder.RegisterType<MessageStore>().AsImplementedInterfaces().SingleInstance();
        }
    }
}
