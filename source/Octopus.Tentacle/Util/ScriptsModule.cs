using Autofac;
using Octopus.Tentacle.Core.Services.Scripts.Locking;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Util
{
    public class ScriptsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<ScriptIsolationMutex>().SingleInstance();
        }
    }
}