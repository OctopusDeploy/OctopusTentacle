using System;
using Autofac;
using Octopus.Shared.Scripts;
using System.Runtime.InteropServices;

namespace Octopus.Shared.Util
{
    public class ShellModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                builder.RegisterType<Bash>().As<IShell>();
            else
                builder.RegisterType<PowerShell>().As<IShell>();
        }
    }
}