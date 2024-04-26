using System;
using Autofac;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Util
{
    public class ShellModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            
            builder.RegisterType<SilentProcessRunnerWrapper>().As<ISilentProcessRunner>().SingleInstance();

            if (PlatformDetection.IsRunningOnWindows)
                builder.RegisterType<PowerShell>().As<IShell>();
            else
                builder.RegisterType<Bash>().As<IShell>();
        }
    }
}