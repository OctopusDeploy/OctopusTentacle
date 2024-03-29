﻿using System;
using Autofac;

namespace Octopus.Tentacle.Util
{
    public class OctopusFileSystemModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<OctopusPhysicalFileSystem>().As<IOctopusFileSystem>();
        }
    }
}