using System;
using System.Reflection;
using Autofac;
using Module = Autofac.Module;

namespace Octopus.Tentacle.Versioning
{
    public class VersioningModule : Module
    {
        private readonly Assembly versionedAssembly;

        public VersioningModule(Assembly versionedAssembly)
        {
            this.versionedAssembly = versionedAssembly;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new AppVersion(versionedAssembly));
        }
    }
}