using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using Autofac;
using NLog;
using NLog.Targets;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Diagnostics.KnowledgeBase;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public abstract class OctopusProgram
    {
        public enum ExitCode
        {
            UnknownCommand = -1,
            Success = 0,
            ControlledFailureException = 1,
            SecurityException = 42,
            ReflectionTypeLoadException = 43,
            GeneralException = 100
        }

        static readonly string StdOutTargetName = "stdout";
        static readonly string StdErrTargetName = "stderr";

        readonly ILog log = Log.Octopus();
        readonly string displayName;
        readonly string version;
        readonly string informationalVersion;
        readonly string[] environmentInformation;
        readonly OptionSet commonOptions;
        IContainer container;
        ICommand commandFromCommandLine;
        ICommand responsibleCommand;
        string[] commandLineArguments;
        bool helpSwitchProvidedInCommandArguments;

        protected OctopusProgram(string displayName, string version, string informationalVersion, string[] environmentInformation, string[] commandLineArguments)
        {
            this.commandLineArguments = commandLineArguments;
            this.displayName = displayName;
            this.version = version;
            this.informationalVersion = informationalVersion;
            this.environmentInformation = environmentInformation;
            commonOptions = new OptionSet();
            commonOptions.Add("nologo", "DEPRECATED: Don't print title or version information. This switch is no longer required, but we want to leave it around so automation scripts don't break.", v => {}, hide: true);
            commonOptions.Add("noconsolelogging", "DEPRECATED: Don't log informational messages to the console (stdout) - errors are still logged to stderr. This switch has been deprecated since it is no longer required. We want to leave it around so automation scripts don't break.", v =>
            {
                DisableConsoleLogging();
            }, hide: true);
            commonOptions.Add("help", "Show detailed help for this command", v => { helpSwitchProvidedInCommandArguments = true; });
        }

        public IContainer Container
        {
            get
            {
                if (container == null)
                    throw new ApplicationException("The container has not yet been initialized. Please do not attempt to access the container until later in the application startup lifecycle.");

                return container;
            }
        }

        public int Run()
        {
            // Initialize logging as soon as possible - waiting for the Container to be built is too late
            InitializeLogging();

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                if (Debugger.IsAttached) Debugger.Break();
                log.ErrorFormat(args.Exception.UnpackFromContainers(), "Unhandled task exception occurred: {0}", args.Exception.PrettyPrint(false));
                args.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;

            int exitCode;
            ICommandHost host = null;
            try
            {
                EnsureTempPathIsWriteable();

                commandLineArguments = ProcessCommonOptions(commonOptions, commandLineArguments, log);

                // Write diagnostics information early as possible - note this will target the global log file since we haven't loaded the instance yet.
                // This is nice because the global log file will always have a history of every application invocation, regardless of instance
                // See: OctopusLogsDirectoryRenderer.DefaultLogsDirectory
                var instanceName = TryLoadInstanceNameFromCommandLineArguments(commandLineArguments);
                WriteDiagnosticsInfoToLogFile(instanceName);

                log.Trace("Creating and configuring the Autofac container");
                container = BuildContainer(instanceName);

                // Try to load the instance here so we can log into the instance's log file as soon as possible
                // If we can't load it, that's OK, we might be creating the instance, or we'll fail with the same error later on when we try to load the instance for real
                if (container.Resolve<IApplicationInstanceSelector>().TryGetCurrentInstance(out var instance))
                {
                    WriteDiagnosticsInfoToLogFile(instance.InstanceName);
                }

                // Now register extensions and their modules into the container
                RegisterAdditionalModules(container);

                // This means we should have the full gamut of all available commands, let's try resolve that now
                commandLineArguments = TryResolveCommand(
                    container.Resolve<ICommandLocator>(),
                    commandLineArguments,
                    helpSwitchProvidedInCommandArguments,
                    out commandFromCommandLine,
                    out responsibleCommand);

                // Suppress logging as soon as practical
                if (responsibleCommand.SuppressConsoleLogging) DisableConsoleLogging();
                
                // Now we should have everything we need to select the most appropriate host and run the responsible command
                commandLineArguments = ParseCommandHostArgumentsFromCommandLineArguments(commandLineArguments, out var forceConsoleHost);

                host = SelectMostAppropriateHost(responsibleCommand, displayName, log, forceConsoleHost);
                host.Run(Start, Stop);

                // If we make it to here we can set the error code as either an UnknownCommand for which you got some help, or Success!
                exitCode = (int)(commandFromCommandLine == null ? ExitCode.UnknownCommand : ExitCode.Success);
            }
            catch (ControlledFailureException ex)
            {
                log.Fatal(ex.Message);
                exitCode = (int)ExitCode.ControlledFailureException;
            }
            catch (SecurityException ex)
            {
                log.Fatal(ex, "A security exception was encountered. Please try re-running the command as an Administrator from an elevated command prompt.");
                log.Fatal(ex);
                exitCode = (int)ExitCode.SecurityException;
            }
            catch (ReflectionTypeLoadException ex)
            {
                log.Fatal(ex);

                foreach (var loaderException in ex.LoaderExceptions)
                {
                    log.Error(loaderException);

                    if (!(loaderException is FileNotFoundException))
                        continue;

                    var exFileNotFound = loaderException as FileNotFoundException;
                    if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                    {
                        log.ErrorFormat("Fusion log: {0}", exFileNotFound.FusionLog);
                    }
                }

                exitCode = (int)ExitCode.ReflectionTypeLoadException;
            }
            catch (Exception ex)
            {
                var unpacked = ex.UnpackFromContainers();
                log.Error(new string('=', 79));
                log.Fatal(unpacked.PrettyPrint());

                ExceptionKnowledgeBaseEntry entry;
                if (ExceptionKnowledgeBase.TryInterpret(unpacked, out entry))
                {
                    log.Error(new string('=', 79));
                    log.Error(entry.Summary);
                    if (entry.HelpText != null || entry.HelpLink != null)
                    {
                        log.Error(new string('-', 79));
                        if (entry.HelpText != null)
                        {
                            log.Error(entry.HelpText);
                        }
                        if (entry.HelpLink != null)
                        {
                            log.Error($"See: {entry.HelpLink}");
                        }
                    }
                }
                exitCode = (int)ExitCode.GeneralException;
            }

            host?.OnExit(exitCode);

            if (exitCode != (int)ExitCode.Success && Debugger.IsAttached)
                Debugger.Break();
            return exitCode;
        }

        static void InitializeLogging()
        {
            Log.Appenders.Add(new NLogAppender());
            AssertLoggingConfigurationIsCorrect();
        }

        static void AssertLoggingConfigurationIsCorrect()
        {
            var stdout = LogManager.Configuration.FindTargetByName(StdOutTargetName) as ColoredConsoleTarget;
            if (stdout == null)
                throw new Exception($"Invalid logging configuration: missing target '{StdOutTargetName}'");
            if (stdout.ErrorStream)
                throw new Exception($"Invalid logging configuration: {StdOutTargetName} should not be redirecting to stderr.");

            var stderr = LogManager.Configuration.FindTargetByName(StdErrTargetName);
            if (stderr == null)
                throw new Exception($"Invalid logging configuration: missing target '{StdErrTargetName}'");
            if (stdout.ErrorStream)
                throw new Exception($"Invalid logging configuration: {StdErrTargetName} should be redirecting to stderr, but isn't.");

            LogFileOnlyLogger.AssertConfigurationIsCorrect();
        }

        void WriteDiagnosticsInfoToLogFile(string instanceName)
        {
            var executable = Path.GetFileName(Assembly.GetEntryAssembly().FullLocalPath());
            LogFileOnlyLogger.Info($"{executable} version {version} ({informationalVersion}) instance {(string.IsNullOrWhiteSpace(instanceName) ? "Default" : instanceName)}");
            LogFileOnlyLogger.Info($"Environment Information:{Environment.NewLine}" +
                $"  {string.Join($"{Environment.NewLine}  ", environmentInformation)}");
        }

        static void DisableConsoleLogging()
        {
            // Suppress logging to the console by removing the console logger for stdout
            var c = LogManager.Configuration;

            // Note: this matches the target name in *.nlog
            var stdoutTarget = c.FindTargetByName(StdOutTargetName);
            foreach (var rule in c.LoggingRules)
            {
                rule.Targets.Remove(stdoutTarget);
            }
            LogManager.Configuration = c;
        }

        static string TryLoadInstanceNameFromCommandLineArguments(string[] commandLineArguments)
        {
            var instanceName = string.Empty;
            var options = AbstractStandardCommand.AddInstanceOption(new OptionSet(), v => instanceName = v);
            
            // Ignore the return parameter here, we want to leave the instance option for the responsible command
            // We're just peeking to see if we can load the instance as early as possible
            options.Parse(commandLineArguments);
            return instanceName;
        }

        static string[] ParseCommandHostArgumentsFromCommandLineArguments(string[] commandLineArguments, out bool forceConsoleHost)
        {
            // Sorry for the mess, we can't set the out param in a lambda
            var forceConsole = false;
            var optionSet = ConsoleHost.AddConsoleSwitch(new OptionSet(), v => forceConsole = true);
            // We actually want to remove the --console switch if it was provided since we've parsed it here
            var remainingCommandLineArguments = optionSet.Parse(commandLineArguments).ToArray();
            forceConsoleHost = forceConsole;
            return remainingCommandLineArguments;
        }

        void LogUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            try
            {
                if (Debugger.IsAttached) Debugger.Break();
                var exception = args.ExceptionObject as Exception; // May not actually be one.
                log.FatalFormat(exception, "Unhandled AppDomain exception occurred: {0}", exception?.PrettyPrint() ?? args.ExceptionObject);
            }
            catch (Exception ex)
            {

                try
                {
                    log.Fatal(ex, "Exception logging unhandled exception: ");
                }
                catch
                {
                    // ignored
                }
            }
        }

        static void EnsureTempPathIsWriteable()
        {
            var tempPath = Path.GetTempPath();
            if (!Directory.Exists(tempPath))
                throw new ControlledFailureException($"The temp folder '{tempPath}' does not exist. Ensure the user '{Environment.UserName}' has a valid temp folder.");

            try
            {
                var tempFile = Path.Combine(tempPath, Guid.NewGuid().ToString("N"));
                File.WriteAllText(tempFile, "");
                File.Delete(tempFile);
            }
            catch
            {
                throw new ControlledFailureException($"Could not write to temp folder '{tempPath}'. Ensure the user '{Environment.UserName}' can write to the temp forlder.");
            }
        }

        static ICommandHost SelectMostAppropriateHost(ICommand command, string displayName, ILog log, bool forceConsoleHost)
        {
            log.Trace("Selecting the most appropriate host");

            var commandSupportsConsoleSwitch = ConsoleHost.HasConsoleSwitch(command.Options);
            if (forceConsoleHost && !commandSupportsConsoleSwitch)
            {
                var commandName = command.GetType().Name.Replace("Command", string.Empty);
                log.Warn($"The {ConsoleHost.ConsoleSwitchExample} switch has been deprecated for the {commandName} command because it should always run interactively. This switch will be removed from the {commandName} command in Octopus 4.0. Please remove the {ConsoleHost.ConsoleSwitchExample} switch now to avoid failures after you upgrade to Octopus 4.0.");
            }

            if (!command.CanRunAsService)
            {
                log.Trace($"The {command.GetType().Name} must run interactively; using a console host");
                return new ConsoleHost(displayName);
            }

            if (forceConsoleHost && commandSupportsConsoleSwitch)
            {
                log.Trace($"The {ConsoleHost.ConsoleSwitchExample} switch was provided for a supported command, must run interactively; using a console host");
                return new ConsoleHost(displayName);
            }

#if WINDOWS_SERVICE
            if (Environment.UserInteractive)
            {
                log.Trace("The program is running interactively; using a console host");
                return new ConsoleHost(displayName);
            }
            log.Trace("The program is not running interactively; using a Windows Service host");
            return new WindowsServiceHost();
#else
            log.Trace("The current runtime does not support Windows Services; using a console host");
            return new ConsoleHost(displayName);
#endif
        }

        static string[] ProcessCommonOptions(OptionSet commonOptions, string[] commandLineArguments, ILog log)
        {
            log.Trace("Processing common command-line options");
            return commonOptions.Parse(commandLineArguments).ToArray();
        }

        void Start(ICommandRuntime commandRuntime)
        {
            responsibleCommand.Start(commandLineArguments, commandRuntime, commonOptions);
        }

        static string[] TryResolveCommand(
            ICommandLocator commandLocator,
            string[] commandLineArguments,
            bool showHelpForCommand,
            out ICommand commandFromCommandLine,
            out ICommand responsibleCommand)
        {
            var commandName = ParseCommandName(commandLineArguments);

            var foundCommandMetadata = string.IsNullOrWhiteSpace(commandName) ? null : commandLocator.Find(commandName);
            var cannotFindCommand = foundCommandMetadata == null;

            // <unknowncommand>
            if (cannotFindCommand)
            {
                commandFromCommandLine = null;
                responsibleCommand = commandLocator.Find("help").Value;
                return commandLineArguments;
            }

            // <command> --help
            if (showHelpForCommand)
            {
                commandFromCommandLine = foundCommandMetadata.Value;
                responsibleCommand = commandLocator.Find("help").Value;
                return commandLineArguments;
            }

            // In this case we've found the command, which could be a normal command,
            // or could be the help command if the command line was "help <command>"
            commandFromCommandLine = foundCommandMetadata.Value;
            responsibleCommand = foundCommandMetadata.Value;

            // Strip the command name argument we parsed from the list so the responsible command can simply parse its options
            return commandLineArguments.Skip(1).ToArray();
        }

        protected abstract IContainer BuildContainer(string instanceName);

        protected virtual void RegisterAdditionalModules(IContainer builtContainer)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new CommandModule());
#pragma warning disable 618
            builder.Update(builtContainer);
#pragma warning restore 618
        }

        static string ParseCommandName(string[] args)
        {
            var first = (args.FirstOrDefault() ?? string.Empty).ToLowerInvariant().TrimStart('-', '/');
            return first;
        }

        void Stop()
        {
            if (responsibleCommand != null)
            {
                log.TraceFormat("Sending stop signal to current command");
                responsibleCommand.Stop();
            }

            log.TraceFormat("Disposing of the container");
            container.Dispose();
        }
    }
}