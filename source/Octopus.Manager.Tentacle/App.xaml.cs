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
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Util;
using CertificateGenerator = Octopus.Tentacle.Certificates.CertificateGenerator;
using CertificatesModule = Octopus.Tentacle.Certificates.CertificatesModule;

namespace Octopus.Manager.Tentacle
{
    public partial class App
    {
        const string EventLogSource = "Octopus Tentacle";
        public const string MainWindowTitle = "Tentacle Manager";

        readonly OptionSet commonOptions = new OptionSet();
        bool reconfigure;

        protected override void OnStartup(StartupEventArgs e)
        {
            var systemLog = new SystemLog();
            UnhandledErrorTrapper.Initialize(systemLog);

            if (!ElevationHelper.IsElevated)
            {
                ElevationHelper.Elevate(e.Args);
                Environment.Exit(0);
            }

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12;

            commonOptions.Add("reconfigure", "Reconfigure the targeted services", v => reconfigure = true);

            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var container = ConfigureContainer();
            
            if (!HasPrerequisites(new TentaclePrerequisiteProfile()))
                Environment.Exit(0);

            if (reconfigure)
            {
#pragma warning disable CA1416
                if (!EventLog.SourceExists(EventLogSource))
#pragma warning restore CA1416
                {
#pragma warning disable CA1416
                    EventLog.CreateEventSource(EventLogSource, "Application");
#pragma warning restore CA1416
                }

                ReconfigureTentacleService(container);
            }

            CreateAndShowShell(container);
        }

        public static IContainer ConfigureContainer()
        {
            var builder = new ContainerBuilder();

            builder.RegisterModule(new CertificatesModule());
            builder.RegisterModule(new LoggingModule());
            builder.RegisterModule(new OctopusFileSystemModule());
            builder.RegisterType<CertificateGenerator>().As<ICertificateGenerator>();
            builder.RegisterModule(new ManagerConfigurationModule(ApplicationName.Tentacle));

            builder.RegisterModule(new TentacleConfigurationModule());
            builder.RegisterModule(new TentacleManagerModule());
            return builder.Build();
        }

        static bool HasPrerequisites(IPrerequisiteProfile profile)
        {
            return new PreReqWindow(profile).ShowDialog() == true;
        }

        void ReconfigureTentacleService(IComponentContext container)
        {
            var applicationInstanceLocator = container.Resolve<IApplicationInstanceStore>();
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
            var shell = CreateShell(container);
            MainWindow = shell;
            shell.ShowDialog();
            Shutdown(0);
        }
        
        static ShellView CreateShell(IComponentContext container)
        {
            var tentacleViewModel = container.Resolve<TentacleManagerModel>();
            
            var tentacleManagerView = new TentacleManagerView(
                tentacleViewModel,
                container.Resolve<InstanceSelectionModel>(),
                container.Resolve<IApplicationInstanceManager>(),
                container.Resolve<IApplicationInstanceStore>());
            
            var shell = new ShellView(MainWindowTitle, tentacleViewModel);
            shell.EnableInstanceSelection();
            shell.Height = 550;
            shell.SetViewContent(tentacleManagerView);
            return shell;
        }
    }
}
