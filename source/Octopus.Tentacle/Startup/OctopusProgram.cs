using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using NLog;
using NLog.Config;
using NLog.Targets;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Diagnostics.KnowledgeBase;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Startup
{
    public abstract class OctopusProgram
    {
        public enum ExitCode : byte
        {
            UnknownCommand = 2,
            Success = 0,
            ControlledFailureException = 1,
            SecurityException = 42,
            ReflectionTypeLoadException = 43,
            GeneralException = 100
        }

        static readonly string StdOutTargetName = "stdout";
        static readonly string StdErrTargetName = "stderr";

        readonly ISystemLog log = new SystemLog();
        readonly string displayName;
        readonly string version;
        readonly string informationalVersion;
        readonly string[] environmentInformation;
        // readonly OptionSet commonOptions;
        // IContainer? container;
        // ICommand? commandFromCommandLine;
        // ICommand? responsibleCommand;
        readonly string[] commandLineArguments;
        // bool helpSwitchProvidedInCommandArguments;

        protected OctopusProgram(string displayName,
            string version,
            string informationalVersion,
            string[] environmentInformation,
            string[] commandLineArguments)
        {
            this.commandLineArguments = commandLineArguments;
            this.displayName = displayName;
            this.version = version;
            this.informationalVersion = informationalVersion;
            this.environmentInformation = environmentInformation;
            // commonOptions = new OptionSet();
            // commonOptions.Add("nologo", "DEPRECATED: Don't print title or version information. This switch is no longer required, but we want to leave it around so automation scripts don't break.", v => { }, true);
            // commonOptions.Add("noconsolelogging",
            //     "DEPRECATED: Don't log informational messages to the console (stdout) - errors are still logged to stderr. This switch has been deprecated since it is no longer required. We want to leave it around so automation scripts don't break.",
            //     v =>
            //     {
            //         DisableConsoleLogging();
            //     },
            //     true);
            // commonOptions.Add("help", "Show detailed help for this command", v => { helpSwitchProvidedInCommandArguments = true; });
        }

        protected abstract ApplicationName ApplicationName { get; }

        public int Run()
        {
            var delayedLog = new DelayedLog();
            // Need to clean up old files before anything else as they may interfere with initialization
            CleanFileSystem(delayedLog);

            // Initialize logging as soon as possible - waiting for the Container to be built is too late
            InitializeLogging();
            delayedLog.FlushTo(log);

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                if (Debugger.IsAttached) Debugger.Break();
                if (args.Exception == null)
                    log.WarnFormat("Unhandled task exception occurred but no exception was supplied");
                else
                    log.WarnFormat(args.Exception.UnpackFromContainers(), "Unhandled task exception occurred: {0}", args.Exception.PrettyPrint(false));
                args.SetObserved();
            };

            AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;

            int exitCode;
            ICommandHost? host = null;
            try
            {
                EnsureTempPathIsWriteable();


                var commandFromCommandLine = new Tentacle()
                    .RunTentacle(commandLineArguments,
                        DisableConsoleLogging,
                        WriteDiagnosticsInfoToLogFile,
                        (shutdown) => new ControlHandler(shutdown, log),
                        new CommandHostStrategy(),
                        displayName,
                        log
                        );
                exitCode = (int)(commandFromCommandLine == null ? ExitCode.UnknownCommand : ExitCode.Success);
            }
            catch (DependencyResolutionException ex) when (ex.InnerException is ControlledFailureException)
            {
                exitCode = HandleException((ControlledFailureException)ex.InnerException);
            }
            catch (ControlledFailureException ex)
            {
                exitCode = HandleException(ex);
            }
            catch (SecurityException ex)
            {
                exitCode = HandleException(ex);
            }
            catch (ReflectionTypeLoadException ex)
            {
                exitCode = HandleException(ex);
            }
            catch (Exception ex)
            {
                exitCode = HandleException(ex);
            }

            host?.OnExit(exitCode);

            LogManager.Shutdown();

            if (exitCode != (int)ExitCode.Success && Debugger.IsAttached)
                Debugger.Break();
            return exitCode;
        }

       

        int HandleException(Exception ex)
        {
            var unpacked = ex.UnpackFromContainers();

            if (ExceptionKnowledgeBase.TryInterpret(unpacked, out var entry) && entry != null)
            {
                if (entry.LogException)
                {
                    log.Error(new string('=', 79));
                    log.Fatal(unpacked.PrettyPrint());
                }
                else
                {
                    LogFileOnlyLogger.Current.Fatal(unpacked.PrettyPrint());
                }

                log.Error(new string('=', 79));
                log.Error(entry.Summary);
                if (entry.HelpText != null || entry.HelpLink != null)
                {
                    log.Error(new string('-', 79));
                    if (entry.HelpText != null)
                        log.Error(entry.HelpText);

                    if (entry.HelpLink != null)
                        log.Error($"See: {entry.HelpLink}");
                }
            }
            else
            {
                log.Error(new string('=', 79));
                log.Fatal(unpacked.PrettyPrint());
            }

            return (int)ExitCode.GeneralException;
        }

        int HandleException(ReflectionTypeLoadException ex)
        {
            log.Fatal(ex);

            if (ex.LoaderExceptions != null)
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    if (loaderException != null)
                        log.Error(loaderException);

                    if (!(loaderException is FileNotFoundException))
                        continue;

                    if (loaderException is FileNotFoundException exFileNotFound && !string.IsNullOrEmpty(exFileNotFound.FusionLog))
                        log.ErrorFormat("Fusion log: {0}", exFileNotFound.FusionLog);
                }

            return (int)ExitCode.ReflectionTypeLoadException;
        }

        int HandleException(SecurityException ex)
        {
            log.Fatal(ex, "A security exception was encountered. Please try re-running the command as an Administrator from an elevated command prompt.");
            log.Fatal(ex);
            return (int)ExitCode.SecurityException;
        }

        int HandleException(ControlledFailureException ex)
        {
            log.Fatal(ex.Message);
            return (int)ExitCode.ControlledFailureException;
        }

        static void CleanFileSystem(ISystemLog log)
        {
            var fileSystem = new OctopusPhysicalFileSystem(log);
            var fileSystemCleaner = new FileSystemCleaner(fileSystem, log);
            fileSystemCleaner.Clean(FileSystemCleaner.PathsToDeleteOnStartupResource);
        }

        void InitializeLogging()
        {
#if !NLOG_HAS_EVENT_LOG_TARGET
            if (PlatformDetection.IsRunningOnWindows)
                Target.Register<EventLogTarget>("EventLog");
            else
                Target.Register<NullLogTarget>("EventLog");
#endif
#if REQUIRES_EXPLICIT_LOG_CONFIG
            var nLogFile = Path.ChangeExtension(GetType().Assembly.Location, "exe.nlog");
            LogManager.ThrowConfigExceptions = true;
            LogManager.Configuration = new XmlLoggingConfiguration(nLogFile);
#endif
            SystemLog.Appenders.Add(new NLogAppender());
            AssertLoggingConfigurationIsCorrect();
        }

        static void AssertLoggingConfigurationIsCorrect()
        {
            var stdout = LogManager.Configuration.FindTargetByName(StdOutTargetName) as ColoredConsoleTarget;
            if (stdout == null)
                throw new Exception($"Invalid logging configuration: missing target '{StdOutTargetName}'");
            if (stdout.StdErr)
                throw new Exception($"Invalid logging configuration: {StdOutTargetName} should not be redirecting to stderr.");

            var stderr = LogManager.Configuration.FindTargetByName(StdErrTargetName);
            if (stderr == null)
                throw new Exception($"Invalid logging configuration: missing target '{StdErrTargetName}'");
            if (stdout.StdErr)
                throw new Exception($"Invalid logging configuration: {StdErrTargetName} should be redirecting to stderr, but isn't.");

            LogFileOnlyLogger.AssertConfigurationIsCorrect();
        }

        void WriteDiagnosticsInfoToLogFile(StartUpInstanceRequest startupRequest)
        {
            var persistedRequest = startupRequest as StartUpRegistryInstanceRequest;
            var fullProcessPath = Assembly.GetEntryAssembly()?.FullProcessPath() ?? throw new Exception("Could not get path of the entry assembly");
            var executable = PlatformDetection.IsRunningOnWindows
                ? Path.GetFileNameWithoutExtension(fullProcessPath)
                : Path.GetFileName(fullProcessPath);
            LogFileOnlyLogger.Current.Info($"{executable} version {version} ({informationalVersion}) instance {(string.IsNullOrWhiteSpace(persistedRequest?.InstanceName) ? "Default" : persistedRequest?.InstanceName)}");
            LogFileOnlyLogger.Current.Info($"Environment Information:{Environment.NewLine}" +
                $"  {string.Join($"{Environment.NewLine}  ", environmentInformation)}");
        }

        static void DisableConsoleLogging()
        {
            // Suppress logging to the console by removing the console logger for stdout
            var c = LogManager.Configuration;

            // Note: this matches the target name in *.nlog
            var stdoutTarget = c.FindTargetByName(StdOutTargetName);
            foreach (var rule in c.LoggingRules)
                rule.Targets.Remove(stdoutTarget);

            LogManager.Configuration = c;
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

        void LogUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            try
            {
                if (Debugger.IsAttached) Debugger.Break();
                var exception = args.ExceptionObject as Exception; // May not actually be one.
                if (exception == null)
                    log.FatalFormat("Unhandled AppDomain exception occurred: {0}", args.ExceptionObject);
                else
                    log.FatalFormat(exception, "Unhandled AppDomain exception occurred: {0}", exception.PrettyPrint());
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

        

        

        // void Start(ICommandRuntime commandRuntime)
        // {
        //     if (responsibleCommand == null)
        //         throw new InvalidOperationException("Responsible command not set");
        //     responsibleCommand.Start(commandLineArguments, commandRuntime, commonOptions);
        // }
        //
        // protected abstract IContainer BuildContainer(StartUpInstanceRequest startUpInstanceRequest);
        //
        //
        //
        // readonly object singleShutdownLock = new object();
        //
        // void Shutdown()
        // {
        //     if (!Monitor.TryEnter(singleShutdownLock)) return;
        //     if (responsibleCommand != null)
        //     {
        //         log.TraceFormat("Sending stop signal to current command");
        //         responsibleCommand.Stop();
        //     }
        //
        //     log.TraceFormat("Disposing of the container");
        //     container?.Dispose();
        // }
        

    }
}
