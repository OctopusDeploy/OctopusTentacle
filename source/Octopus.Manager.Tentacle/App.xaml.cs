using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows;
using Autofac;
using Octopus.Manager.Tentacle.Dialogs;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.PreReq;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
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
                container.Resolve<IComponentContext>(),
                tentacleViewModel,
                container.Resolve<InstanceSelectionModel>(),
                container.Resolve<IApplicationInstanceManager>(),
                container.Resolve<IApplicationInstanceStore>(),
                container.Resolve<TentacleSetupWizardLauncher>());
            
            var shell = new ShellView("Tentacle Manager", tentacleViewModel);
            shell.EnableInstanceSelection();
            shell.Height = 550;
            shell.SetViewContent(tentacleManagerView);
            return shell;
        }
    }
}
