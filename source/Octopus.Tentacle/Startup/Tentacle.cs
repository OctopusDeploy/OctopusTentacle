using System;
using System.Linq;
using System.Threading;
using Autofac;
using Octopus.Diagnostics;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Services;
using Octopus.Tentacle.Time;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Versioning;

namespace Octopus.Tentacle.Startup
{
    public class Tentacle
    {
        
        public ICommand? RunTentacle(string[] commandLineArguments,
            Action disableConsoleLogging,
            Action<StartUpInstanceRequest> logDiagnosticsInfo,
            Func<Action, IDisposable> controlCHandlerFactory,
            ICommandHostStrategy commandHostStrategy,
            string displayName,
            ISystemLog log)
        {
            var helpSwitchProvidedInCommandArguments = false;
            var commonOptions = CommonOptions(() => helpSwitchProvidedInCommandArguments = true, disableConsoleLogging);
            commandLineArguments = ProcessCommonOptions(commonOptions, commandLineArguments, log);

            // Write diagnostics information early as possible - note this will target the global log file since we haven't loaded the instance yet.
            // This is nice because the global log file will always have a history of every application invocation, regardless of instance
            // See: OctopusLogsDirectoryRenderer.DefaultLogsDirectory
            var startupRequest = TryLoadInstanceNameFromCommandLineArguments(commandLineArguments);
            logDiagnosticsInfo(startupRequest);

            log.Trace("Creating and configuring the Autofac container");
            var container = BuildContainer(startupRequest);

            // Try to load the instance here so we can configure and log into the instance's log file as soon as possible
            // If we can't load it, that's OK, we might be creating the instance, or we'll fail with the same error later on when we try to load the instance for real
            var instanceSelector = container.Resolve<IApplicationInstanceSelector>();
            if (instanceSelector.CanLoadCurrentInstance())
            {
                container.Resolve<LogInitializer>().Start();
                logDiagnosticsInfo(startupRequest);
            }

            // Now register extensions and their modules into the container
            RegisterAdditionalModules(container);

            // This means we should have the full gamut of all available commands, let's try resolve that now
            ICommand? commandFromCommandLine;
            ICommand? responsibleCommand;
            commandLineArguments = TryResolveCommand(
                container.Resolve<ICommandLocator>(),
                commandLineArguments,
                helpSwitchProvidedInCommandArguments,
                out commandFromCommandLine,
                out responsibleCommand);

            // Suppress logging as soon as practical
            if (responsibleCommand.SuppressConsoleLogging) disableConsoleLogging();

            // Now we should have everything we need to select the most appropriate host and run the responsible command
            commandLineArguments = ParseCommandHostArgumentsFromCommandLineArguments(
                commandLineArguments,
                out var forceConsoleHost,
                out var forceNoninteractiveHost,
                out var monitorMutexHost);

            var host = commandHostStrategy.SelectMostAppropriateHost(responsibleCommand,
                displayName,
                log,
                forceConsoleHost,
                forceNoninteractiveHost,
                monitorMutexHost);

            var singleShutdownLock = new object();

            Action shutdown = () => Shutdown(singleShutdownLock, container, responsibleCommand, log);
            using (var _ = controlCHandlerFactory(shutdown))
            {
                host.Run(commandRuntime => Start(commandLineArguments, commandRuntime, commonOptions, responsibleCommand), shutdown);
            }

            return commandFromCommandLine;
        }

        static string[] ProcessCommonOptions(OptionSet commonOptions, string[] commandLineArguments, ISystemLog log)
        {
            log.Trace("Processing common command-line options");
            return commonOptions.Parse(commandLineArguments).ToArray();
        }

        StartUpInstanceRequest TryLoadInstanceNameFromCommandLineArguments(string[] commandLineArguments)
        {
            var instanceName = string.Empty;
            var configFile = string.Empty;

            var options = AbstractStandardCommand.AddInstanceOption(new OptionSet(), v => instanceName = v, v => configFile = v);

            // Ignore the return parameter here, we want to leave the instance option for the responsible command
            // We're just peeking to see if we can load the instance as early as possible
            options.Parse(commandLineArguments);

            if (!string.IsNullOrWhiteSpace(instanceName))
                return new StartUpRegistryInstanceRequest(instanceName);
            if (!string.IsNullOrWhiteSpace(configFile))
                return new StartUpConfigFileInstanceRequest(configFile);

            return new StartUpDynamicInstanceRequest();
        }

        protected virtual void RegisterAdditionalModules(IContainer builtContainer)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new CommandModule());
#pragma warning disable 618
            builder.Update(builtContainer);
#pragma warning restore 618
        }

        void Start(string[] commandLineArguments, ICommandRuntime commandRuntime, OptionSet commonOptions, ICommand? responsibleCommand)
        {
            if (responsibleCommand == null)
                throw new InvalidOperationException("Responsible command not set");
            responsibleCommand.Start(commandLineArguments, commandRuntime, commonOptions);
        }

        void Shutdown(object singleShutdownLock, IContainer container, ICommand? responsibleCommand, ISystemLog log)
        {
            if (!Monitor.TryEnter(singleShutdownLock)) return;
            if (responsibleCommand != null)
            {
                log.TraceFormat("Sending stop signal to current command");
                responsibleCommand.Stop();
            }

            log.TraceFormat("Disposing of the container");
            container?.Dispose();
        }

        static string[] TryResolveCommand(
            ICommandLocator commandLocator,
            string[] commandLineArguments,
            bool showHelpForCommand,
            out ICommand? commandFromCommandLine,
            out ICommand responsibleCommand)
        {
            var commandName = ParseCommandName(commandLineArguments);

            var foundCommandMetadata = string.IsNullOrWhiteSpace(commandName)
                ? null
                : commandLocator.Find(commandName);

            // <unknowncommand>
            if (foundCommandMetadata == null)
            {
                commandFromCommandLine = null;
                responsibleCommand = commandLocator.Find("help")!.Value;
                return commandLineArguments;
            }

            // <command> --help
            if (showHelpForCommand)
            {
                commandFromCommandLine = foundCommandMetadata?.Value;
                responsibleCommand = commandLocator.Find("help")!.Value;
                return commandLineArguments;
            }

            // In this case we've found the command, which could be a normal command,
            // or could be the help command if the command line was "help <command>"
            commandFromCommandLine = foundCommandMetadata.Value;
            responsibleCommand = foundCommandMetadata.Value;

            // Strip the command name argument we parsed from the list so the responsible command can simply parse its options
            return commandLineArguments.Skip(1).ToArray();
        }

        static string ParseCommandName(string[] args)
        {
            var first = (args.FirstOrDefault() ?? string.Empty).ToLowerInvariant().TrimStart('-', '/');
            return first;
        }

        public static string[] ParseCommandHostArgumentsFromCommandLineArguments(
            string[] commandLineArguments,
            out bool forceConsoleHost,
            out bool forceNoninteractiveHost,
            out string? monitorMutexHost)
        {
            // Sorry for the mess, we can't set the out param in a lambda
            var forceConsole = false;
            var optionSet = ConsoleHost.AddConsoleSwitch(new OptionSet(), v => forceConsole = true);
            string? monitorMutex = null;
            var forceNoninteractive = false;
            optionSet.Add("noninteractive", v => forceNoninteractive = true);
            optionSet.Add("monitorMutex=", v => monitorMutex = v);

            // We actually want to remove the --console switch if it was provided since we've parsed it here
            var remainingCommandLineArguments = optionSet.Parse(commandLineArguments).ToArray();
            forceConsoleHost = forceConsole;
            monitorMutexHost = monitorMutex;
            forceNoninteractiveHost = forceNoninteractive;
            return remainingCommandLineArguments;
        }

        public OptionSet CommonOptions(Action helpRequested, Action disableConsoleLogging)
        {
            var commonOptions = new OptionSet();
            commonOptions.Add("nologo", "DEPRECATED: Don't print title or version information. This switch is no longer required, but we want to leave it around so automation scripts don't break.", v =>
            {
            }, true);
            commonOptions.Add("noconsolelogging",
                "DEPRECATED: Don't log informational messages to the console (stdout) - errors are still logged to stderr. This switch has been deprecated since it is no longer required. We want to leave it around so automation scripts don't break.",
                v =>
                {
                    disableConsoleLogging();
                },
                true);
            commonOptions.Add("help", "Show detailed help for this command", v => helpRequested());
            return commonOptions;
        }

        public IContainer BuildContainer(StartUpInstanceRequest startUpInstanceRequest)
        {
            var builder = new ContainerBuilder();

            var applicationName = ApplicationName.Tentacle;

            builder.RegisterModule(new ShellModule());
            builder.RegisterModule(new ConfigurationModule(applicationName, startUpInstanceRequest));
            builder.RegisterModule(new TentacleConfigurationModule());
            builder.RegisterModule(new LogMaskingModule());
            builder.RegisterModule(new OctopusClientInitializerModule());
            builder.RegisterModule(new LoggingModule());
            builder.RegisterModule(new OctopusFileSystemModule());
            builder.RegisterModule(new CertificatesModule());
            builder.RegisterModule(new TimeModule());
            builder.RegisterModule(new ClientModule());
            builder.RegisterModule(new TentacleCommunicationsModule());
            builder.RegisterModule(new ServicesModule());
            builder.RegisterModule(new VersioningModule(GetType().Assembly));

            builder.RegisterCommand<CreateInstanceCommand>("create-instance", "Registers a new instance of the Tentacle service");
            builder.RegisterCommand<DeleteInstanceCommand>("delete-instance", "Deletes an instance of the Tentacle service");
            builder.RegisterCommand<WatchdogCommand>("watchdog", "Configure a scheduled task to monitor the Tentacle service(s)")
                .WithParameter("applicationName", applicationName);
            builder.RegisterCommand<CheckServicesCommand>("checkservices", "Checks the Tentacle instances are running")
                .WithParameter("applicationName", applicationName);
            builder.RegisterCommand<RunAgentCommand>("agent", "Starts the Tentacle Agent in debug mode", "", "run");
            builder.RegisterCommand<ConfigureCommand>("configure", "Sets Tentacle settings such as the port number and thumbprints");
            builder.RegisterCommand<UpdateTrustCommand>("update-trust", "Replaces the trusted Octopus Server thumbprint of any matching polling or listening registrations with a new thumbprint to trust");
            builder.RegisterCommand<RegisterMachineCommand>("register-with", "Registers this machine as a deployment target with an Octopus Server");
            builder.RegisterCommand<RegisterWorkerCommand>("register-worker", "Registers this machine as a worker with an Octopus Server");
            builder.RegisterCommand<ExtractCommand>("extract", "Extracts a NuGet package");
            builder.RegisterCommand<DeregisterMachineCommand>("deregister-from", "Deregisters this deployment target from an Octopus Server");
            builder.RegisterCommand<DeregisterWorkerCommand>("deregister-worker", "Deregisters this worker from an Octopus Server");
            builder.RegisterCommand<NewCertificateCommand>("new-certificate", "Creates and installs a new certificate for this Tentacle");
            builder.RegisterCommand<ShowThumbprintCommand>("show-thumbprint", "Show the thumbprint of this Tentacle's certificate");
            builder.RegisterCommand<ServiceCommand>("service", "Start, stop, install and configure the Tentacle service")
                .WithParameter("applicationName", applicationName)
                .WithParameter("serviceDescription", "Octopus Deploy: Tentacle deployment agent")
                .WithParameter("assemblyContainingService", typeof(Program).Assembly);
            builder.RegisterCommand<ProxyConfigurationCommand>("proxy", "Configure the HTTP proxy used by Octopus");
            builder.RegisterCommand<PollingProxyConfigurationCommand>("polling-proxy", "Configure the HTTP proxy used by polling Tentacles to reach the Octopus Server");
            builder.RegisterCommand<ServerCommsCommand>("server-comms", "Configure how the Tentacle communicates with an Octopus Server");
            builder.RegisterCommand<ImportCertificateCommand>("import-certificate", "Replace the certificate that Tentacle uses to authenticate itself");
            builder.RegisterCommand<PollCommand>("poll-server", "Configures an Octopus Server that this Tentacle will poll");
            builder.RegisterCommand<ListInstancesCommand>("list-instances", "Lists all installed Tentacle instances");
            builder.RegisterCommand<VersionCommand>("version", "Show the Tentacle version information");
            builder.RegisterCommand<ShowConfigurationCommand>("show-configuration", "Outputs the Tentacle configuration");

            return builder.Build();
        }
    }
}
