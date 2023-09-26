using Autofac;

namespace Octopus.Tentacle.Maintenance
{
    public class MaintenanceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<WorkspaceCleanerConfiguration>().SingleInstance();
            builder.RegisterType<WorkspaceCleaner>().SingleInstance();
            builder.RegisterType<WorkspaceCleanerTask>().As<IWorkspaceCleanerTask>().SingleInstance();
        }
    }
}