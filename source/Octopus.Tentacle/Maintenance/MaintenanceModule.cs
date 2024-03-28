using Autofac;
using Octopus.Tentacle.Background;

namespace Octopus.Tentacle.Maintenance
{
    public class MaintenanceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<WorkspaceCleanerConfiguration>().SingleInstance();
            builder.RegisterType<WorkspaceCleaner>().SingleInstance();
            builder.RegisterType<WorkspaceCleanerTask>().As<IWorkspaceCleanerTask>().As<IBackgroundTask>().SingleInstance();
        }
    }
}