using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows;
using Autofac;
using Octopus.Manager.Tentacle.Dialogs;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.PreReq;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration;
using Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Security;
using Octopus.Shared.Util;
using Octopus.Tentacle.Configuration;

namespace Octopus.Manager.Tentacle
{
    public partial class App
    {
        const string EventLogSource = "Octopus Tentacle";

        readonly OptionSet commonOptions = new OptionSet();
        bool reconfigure;

        protected override void OnStartup(StartupEventArgs e)
        {
            UnhandledErrorTrapper.Initialize();

            if (!ElevationHelper.IsElevated)
            {
                ElevationHelper.Elevate(e.Args);
                Environment.Exit(0);
            }

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Ssl3
                | SecurityProtocolType.Tls
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12;

            commonOptions.Add("reconfigure", "Reconfigure the targeted services", v => reconfigure = true);

            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var container = ConfigureContainer();

            if (reconfigure)
            {
                if (!EventLog.SourceExists(EventLogSource))
                {
                    EventLog.CreateEventSource(EventLogSource, "Application");
                }

                ReconfigureTentacleService(container);
            }

            CreateAndShowShell(container);
        }

        static IContainer ConfigureContainer()
        {
            var builder = new ContainerBuilder();

            builder.RegisterModule(new CertificatesModule());
            builder.RegisterModule(new LoggingModule());
            builder.RegisterModule(new OctopusFileSystemModule());
            builder.RegisterType<CertificateGenerator>().As<ICertificateGenerator>();
            builder.RegisterType<CommandLineRunner>().As<ICommandLineRunner>();
            builder.RegisterModule(new ManagerConfigurationModule(ApplicationName.Tentacle));

            if (!HasPrerequisites(new TentaclePrerequisiteProfile()))
                Environment.Exit(0);

            builder.RegisterModule(new TentacleConfigurationModule());
            builder.RegisterModule(new TentacleModule());
            return builder.Build();
        }

        static bool HasPrerequisites(IPrerequisiteProfile profile)
        {
            return new PreReqWindow(profile).ShowDialog() == true;
        }

        void ReconfigureTentacleService(IComponentContext container)
        {
            var applicationInstanceLocator = container.Resolve<IApplicationInstanceLocator>();
            var instances = applicationInstanceLocator.ListInstances();
            var defaultInstance = instances.Where(x => x.InstanceName == ApplicationInstanceRecord.GetDefaultInstance(ApplicationName.Tentacle)).ToArray();
            var instancesWithDefaultFirst = defaultInstance.Concat(instances.Except(defaultInstance).OrderBy(x => x.InstanceName));
            foreach (var instance in instancesWithDefaultFirst)
            {
                var model = container.Resolve<TentacleManagerModel>();
                model.Load(instance);
                var isDefaultInstance = instance.InstanceName == ApplicationInstanceRecord.GetDefaultInstance(ApplicationName.Tentacle);
                var title = isDefaultInstance ? "Reconfiguring Tentacle..." : $"Reconfiguring Tentacle {instance.InstanceName}...";
                RunProcessDialog.ShowDialog(MainWindow, model.ServiceWatcher.GetReconfigureCommands(), title, model.LogsDirectory, showOutputLog: true);
            }
        }

        void CreateAndShowShell(IComponentContext container)
        {
            var shell = container.Resolve<ShellView>();
            MainWindow = shell;
            shell.ShowDialog();
            Shutdown(0);
        }
    }
}