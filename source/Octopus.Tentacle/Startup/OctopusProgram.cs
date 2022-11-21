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
        readonly OptionSet commonOptions;
        IContainer? container;
        ICommand? commandFromCommandLine;
        ICommand? responsibleCommand;
        string[] commandLineArguments;
        bool helpSwitchProvidedInCommandArguments;

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
            commonOptions = new OptionSet();
            commonOptions.Add("nologo", "DEPRECATED: Don't print title or version information. This switch is no longer required, but we want to leave it around so automation scripts don't break.", v => { }, true);
            commonOptions.Add("noconsolelogging",
                "DEPRECATED: Don't log informational messages to the console (stdout) - errors are still logged to stderr. This switch has been deprecated since it is no longer required. We want to leave it around so automation scripts don't break.",
                v =>
                {
                    DisableConsoleLogging();
                },
                true);
            commonOptions.Add("help", "Show detailed help for this command", v => { helpSwitchProvidedInCommandArguments = true; });
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

                commandLineArguments = ProcessCommonOptions(commonOptions, commandLineArguments, log);

                // Write diagnostics information early as possible - note this will target the global log file since we haven't loaded the instance yet.
                // This is nice because the global log file will always have a history of every application invocation, regardless of instance
                // See: OctopusLogsDirectoryRenderer.DefaultLogsDirectory
                var startupRequest = TryLoadInstanceNameFromCommandLineArguments(commandLineArguments);
                WriteDiagnosticsInfoToLogFile(startupRequest);

                log.Trace("Creating and configuring the Autofac container");
                container = BuildContainer(startupRequest);

                // Try to load the instance here so we can configure and log into the instance's log file as soon as possible
                // If we can't load it, that's OK, we might be creating the instance, or we'll fail with the same error later on when we try to load the instance for real
                var instanceSelector = container.Resolve<IApplicationInstanceSelector>();
                if (instanceSelector.CanLoadCurrentInstance())
                {
                    container.Resolve<LogInitializer>().Start();
                    WriteDiagnosticsInfoToLogFile(startupRequest);
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
                commandLineArguments = ParseCommandHostArgumentsFromCommandLineArguments(
                    commandLineArguments,
                    out var forceConsoleHost,
                    out var forceNoninteractiveHost,
                    out var monitorMutexHost);

                host = SelectMostAppropriateHost(responsibleCommand,
                    displayName,
                    log,
                    forceConsoleHost,
                    forceNoninteractiveHost,
                    monitorMutexHost);

                RunHost(host);
                // If we make it to here we can set the error code as either an UnknownCommand for which you got some help, or Success!
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

        void RunHost(ICommandHost host)
        {
#if FULL_FRAMEWORK
            /*
             * The handler raises under the following conditions:
             *  - Ctrl+C (CTRL_C_EVENT)
             *  - Closing Window (CTRL_CLOSE_EVENT)
             *  - Docker Stop (CTRL_SHUTDOWN_EVENT)
             */
            var hr = new CtrlSignaling.HandlerRoutine(type =>
            {
                log.Trace("Shutdown signal received: " + type);
                host.Stop(Shutdown);
                return true;
            });
            CtrlSignaling.SetConsoleCtrlHandler(hr, true);
            host.Run(Start, Shutdown);
            GC.KeepAlive(hr);
#else
            Console.CancelKeyPress += (s, e) =>
            {
                //SIGINT (ControlC) and SIGQUIT (ControlBreak)
                log.Trace("CancelKeyPress signal received: " + e.SpecialKey);
                host.Stop(Shutdown);
            };
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                //SIGTERM - i.e. Docker Stop
                log.Trace("AppDomain process exiting");
                host.Stop(Shutdown);
            };
            host.Run(Start, Shutdown);

#endif
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

        static ICommandHost SelectMostAppropriateHost(ICommand command,
            string displayName,
            ISystemLog log,
            bool forceConsoleHost,
            bool forceNoninteractiveHost,
            string? monitorMutexHost)
        {
            log.Trace("Selecting the most appropriate host");

            var commandSupportsConsoleSwitch = ConsoleHost.HasConsoleSwitch(command.Options);

            if (monitorMutexHost != null && !string.IsNullOrEmpty(monitorMutexHost))
            {
                log.Trace("The --monitorMutex switch was provided for a supported command");
                return new MutexHost(monitorMutexHost, log);
            }

            if (forceNoninteractiveHost && commandSupportsConsoleSwitch)
            {
                log.Trace("The --noninteractive switch was provided for a supported command");
                return new NoninteractiveHost();
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

            if (IsRunningAsAWindowsService(log))
            {
                log.Trace("The program is not running interactively; using a Windows Service host");
                return new WindowsServiceHost(log);
            }

            log.Trace("The program is running interactively; using a console host");
            return new ConsoleHost(displayName);
        }

        static bool IsRunningAsAWindowsService(ISystemLog log)
        {
            if (PlatformDetection.IsRunningOnMac || PlatformDetection.IsRunningOnNix)
                return false;

#if USER_INTERACTIVE_DOES_NOT_WORK
            try
            {
                var child = Process.GetCurrentProcess();

                var parentPid = 0;

                var hnd = Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0);

                if (hnd == IntPtr.Zero)
                    return false;

                var processInfo = new Kernel32.PROCESSENTRY32
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(Kernel32.PROCESSENTRY32))
                };

                if (Kernel32.Process32First(hnd, ref processInfo) == false)
                    return false;

                do
                {
                    if (child.Id == processInfo.th32ProcessID)
                        parentPid = (int)processInfo.th32ParentProcessID;
                } while (parentPid == 0 && Kernel32.Process32Next(hnd, ref processInfo));

                if (parentPid <= 0)
                    return false;

                var parent = Process.GetProcessById(parentPid);
                return parent.ProcessName.ToLower() == "services";
            }
            catch (Exception ex)
            {
                log.Trace(ex, "Could not determine whether the parent process was the service host, assuming it isn't");
                return false;
            }
#else
            return !Environment.UserInteractive;
#endif
        }

        static string[] ProcessCommonOptions(OptionSet commonOptions, string[] commandLineArguments, ISystemLog log)
        {
            log.Trace("Processing common command-line options");
            return commonOptions.Parse(commandLineArguments).ToArray();
        }

        void Start(ICommandRuntime commandRuntime)
        {
            if (responsibleCommand == null)
                throw new InvalidOperationException("Responsible command not set");
            responsibleCommand.Start(commandLineArguments, commandRuntime, commonOptions);
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

        protected abstract IContainer BuildContainer(StartUpInstanceRequest startUpInstanceRequest);

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

        readonly object singleShutdownLock = new object();

        void Shutdown()
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

#pragma warning disable PC003 // Native API not available in UWP
#if USER_INTERACTIVE_DOES_NOT_WORK
        static class Kernel32
        {
            public static readonly uint TH32CS_SNAPPROCESS = 2;

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

            [DllImport("kernel32.dll")]
            public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

            [DllImport("kernel32.dll")]
            public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESSENTRY32
            {
                public uint dwSize;
                public readonly uint cntUsage;
                public readonly uint th32ProcessID;
                public readonly IntPtr th32DefaultHeapID;
                public readonly uint th32ModuleID;
                public readonly uint cntThreads;
                public readonly uint th32ParentProcessID;
                public readonly int pcPriClassBase;
                public readonly uint dwFlags;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
                public readonly string szExeFile;
            }
        }
#endif
#pragma warning restore PC003 // Native API not available in UWP

#if FULL_FRAMEWORK
        public static class CtrlSignaling
        {
            public delegate bool HandlerRoutine(CtrlTypes CtrlType);

            public enum CtrlTypes
            {
                CTRL_C_EVENT = 0,
                CTRL_BREAK_EVENT = 1,
                CTRL_CLOSE_EVENT = 2,
                CTRL_LOGOFF_EVENT = 5,
                CTRL_SHUTDOWN_EVENT = 6
            }

            [DllImport("Kernel32.dll")]
            public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
        }
#endif
    }
}
