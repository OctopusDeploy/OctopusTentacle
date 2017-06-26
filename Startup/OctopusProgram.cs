using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using Autofac;
using NLog;
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
            Ok = 0,
            ControlledFailureException = 1,
            SecurityException = 42,
            ReflectionTypeLoadException = 43,
            GeneralException = 100
        }

        readonly ILogWithContext log = Log.Octopus();
        readonly string displayName;
        readonly string version;
        readonly string informationalVersion;
        readonly string[] environmentInformation;
        readonly OptionSet commonOptions;
        IContainer container;
        ICommand commandInstance;
        string[] commandLineArguments;
        bool forceConsole;
        bool showHelpForCommand;

        protected OctopusProgram(string displayName, string version, string informationalVersion, string[] environmentInformation, string[] commandLineArguments)
        {
            this.commandLineArguments = commandLineArguments;
            this.displayName = displayName;
            this.version = version;
            this.informationalVersion = informationalVersion;
            this.environmentInformation = environmentInformation;
            commonOptions = new OptionSet();
            commonOptions.Add("console", "Don't attempt to run as a service, even if the user is non-interactive", v => forceConsole = true);
            AddNoLogoOption();
            commonOptions.Add("noconsolelogging", "Don't log informational messages to the console (stdout) - errors are still logged to stderr", v => { DisableConsoleLogging(); });
            commonOptions.Add("help", "", v => { showHelpForCommand = true; });
        }

        [ObsoleteEx(Message = "We should consider removing '--nologo'", TreatAsErrorFromVersion = "4.0")]
        void AddNoLogoOption()
        {
            commonOptions.Add("nologo", "Don't print title or version information", v =>
            {
                StartupDiagnosticsLogger.Warn("'--nologo' is being deprecated in a future version since the title and version information are not printed any more.");
            });
        }

        static void DisableConsoleLogging()
        {
            // Suppress logging to the console by removing the console logger for stdout
            var c = LogManager.Configuration;

            // Note: this matches the target name in octopus.server.exe.nlog
            var stdoutTarget = c.FindTargetByName("stdout");
            foreach (var rule in c.LoggingRules)
            {
                rule.Targets.Remove(stdoutTarget);
            }
            LogManager.Configuration = c;
        }

        protected OptionSet CommonOptions => commonOptions;

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
            Log.Appenders.Add(new NLogAppender());
            
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

                commandLineArguments = ProcessCommonOptions();

                // Write diagnostics information early as possible - note this will target the global log file since we haven't loaded the instance yet.
                // This is nice because the global log file will always have a history of every application invocation, regardless of instance
                // See: OctopusLogsDirectoryRenderer.DefaultLogsDirectory
                var instanceName = TryLoadInstanceNameFromCommandLineArguments(commandLineArguments);
                WriteDiagnosticsInfoToLogFile(instanceName);

                log.Trace("Creating and configuring the Autofac container");
                container = BuildContainer(instanceName);

                // Try to load the instance here so we can log into the instance's log file as soon as possible
                // If we can't load it, that's OK, we might be creating the instance, or we'll fail with the same error later on when we try to load the instance for real
                if (container.Resolve<IApplicationInstanceSelector>().TryLoadCurrentInstance(out var instance))
                {
                    WriteDiagnosticsInfoToLogFile(instance.InstanceName);
                }

                RegisterAdditionalModules(container);

                host = SelectMostAppropriateHost();
                host.Run(Start, Stop);
                exitCode = Environment.ExitCode;
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

            if (exitCode > 1 && Debugger.IsAttached)
                Debugger.Break();
            return exitCode;
        }

        void WriteDiagnosticsInfoToLogFile(string instanceName)
        {
            StartupDiagnosticsLogger.Info($"Starting {displayName} version {version} ({informationalVersion}) instance {(string.IsNullOrWhiteSpace(instanceName) ? "Default" : instanceName)}");
            StartupDiagnosticsLogger.Info($"Environment Information:{Environment.NewLine}" +
                $"  {string.Join($"{Environment.NewLine}  ", environmentInformation)}");
        }

        static string TryLoadInstanceNameFromCommandLineArguments(string[] arguments)
        {
            var instanceName = string.Empty;
            new OptionSet {{"instance=", "Name of the instance to use", v => instanceName = v}}.Parse(arguments);
            return instanceName;
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

        ICommandHost SelectMostAppropriateHost()
        {
            log.Trace("Selecting the most appropriate host");

            if (forceConsole)
            {
                log.Trace("The --console switch was passed; using a console host");
                return new ConsoleHost(displayName);
            }

            if (Environment.UserInteractive)
            {
                log.Trace("The program is running interactively; using a console host");
                return new ConsoleHost(displayName);
            }

            log.Trace("The program is not running interactively; using a Windows Service host");
            return new WindowsServiceHost();
        }

        string[] ProcessCommonOptions()
        {
            log.Trace("Processing common command-line options");
            return CommonOptions.Parse(commandLineArguments).ToArray();
        }

        void Start(ICommandRuntime commandRuntime)
        {
            var commandLocator = container.Resolve<ICommandLocator>();

            var commandName = ParseCommandName(commandLineArguments);

            var command = string.IsNullOrWhiteSpace(commandName) ? null : commandLocator.Find(commandName);
            if (command == null)
            {
                command = commandLocator.Find("help");
                Environment.ExitCode = (int)ExitCode.UnknownCommand;
            }
            else if (showHelpForCommand)
            {
                command = commandLocator.Find("help");
            }
            else
            {
                // For all other commands, strip the command name argument from the list
                commandLineArguments = commandLineArguments.Skip(1).ToArray();
            }

            commandInstance = command.Value;

            commandInstance.Start(commandLineArguments, commandRuntime, CommonOptions);
        }

        protected abstract IContainer BuildContainer(string instanceName);

        protected virtual void RegisterAdditionalModules(IContainer builtContainer)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new CommandModule());
            builder.Update(builtContainer);
        }

        public static string ParseCommandName(string[] args)
        {
            var first = (args.FirstOrDefault() ?? string.Empty).ToLowerInvariant().TrimStart('-', '/');
            return first;
        }

        void Stop()
        {
            if (commandInstance != null)
            {
                log.TraceFormat("Sending stop signal to current command");
                commandInstance.Stop();
            }

            log.TraceFormat("Disposing of the container");
            container.Dispose();
        }
    }
}