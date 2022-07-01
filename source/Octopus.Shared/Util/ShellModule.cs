using System;
using Autofac;
using Octopus.Shared.Scripts;

namespace Octopus.Shared.Util
{
    public class ShellModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            if (PlatformDetection.IsRunningOnWindows)
                builder.RegisterType<PowerShell>().As<IShell>();
            else
                builder.RegisterType<Bash>().As<IShell>();
        }
    }
}