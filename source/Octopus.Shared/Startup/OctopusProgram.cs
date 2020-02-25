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
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Diagnostics.KnowledgeBase;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
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

        public int Run()
        {
            var delayedLog = new DelayedLog();
            // Need to clean up old files before anything else as they may interfere with initialization
            CleanFileSystem(delayedLog);

            // Initialize logging as soon as possible - waiting for the Container to be built is too late
            InitializeLogging();
            delayedLog.FlushTo(Log.System());

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                if (Debugger.IsAttached) Debugger.Break();
                log.WarnFormat(args.Exception.UnpackFromContainers(), "Unhandled task exception occurred: {0}", args.Exception.PrettyPrint(false));
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
                commandLineArguments = ParseCommandHostArgumentsFromCommandLineArguments(
                    commandLineArguments, 
                    out var forceConsoleHost,
                    out var forceNoninteractiveHost,
                    out var monitorMutexHost);

                host = SelectMostAppropriateHost(responsibleCommand, displayName, log, forceConsoleHost, forceNoninteractiveHost, monitorMutexHost);
                
                RunHost(host);
                // If we make it to here we can set the error code as either an UnknownCommand for which you got some help, or Success!
                exitCode = (int)(commandFromCommandLine == null ? ExitCode.UnknownCommand : ExitCode.Success);
            }
            catch (DependencyResolutionException ex) when (ex.InnerException is ControlledFailureException)
            {
                exitCode = HandleException(ex.InnerException);
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

        private void RunHost(ICommandHost host)
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
                log.Trace("Shutdown signal received: "+ type);
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
                log.Trace("CancelKeyPress signal received: "+ e.SpecialKey);
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

        private int HandleException(Exception ex)
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

            return (int)ExitCode.GeneralException;
        }

        private int HandleException(ReflectionTypeLoadException ex)
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

            return (int)ExitCode.ReflectionTypeLoadException;
        }

        private int HandleException(SecurityException ex)
        {
            log.Fatal(ex, "A security exception was encountered. Please try re-running the command as an Administrator from an elevated command prompt.");
            log.Fatal(ex);
            return (int)ExitCode.SecurityException;
        }

        private int HandleException(ControlledFailureException ex)
        {
            log.Fatal(ex.Message);
            return (int)ExitCode.ControlledFailureException;
        }

        static void CleanFileSystem(ILog log)
        {
            var fileSystem = new OctopusPhysicalFileSystem();
            var fileSystemCleaner = new FileSystemCleaner(fileSystem, log);
            fileSystemCleaner.Clean(FileSystemCleaner.PathsToDeleteOnStartupResource);
        }

        void InitializeLogging()
        {
            
            
#if !NLOG_HAS_EVENT_LOG_TARGET
            if(PlatformDetection.IsRunningOnWindows) {
                Target.Register<EventLogTarget>("EventLog");
            } else {
                Target.Register<NullLogTarget>("EventLog");
            }
#endif
#if REQUIRES_EXPLICIT_LOG_CONFIG
            var nLogFile = Path.ChangeExtension(GetType().Assembly.Location, "exe.nlog");
            LogManager.Configuration = new XmlLoggingConfiguration(nLogFile, false);
#endif
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
            var executable = PlatformDetection.IsRunningOnWindows
                ? Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().FullProcessPath())
                : Path.GetFileName(Assembly.GetEntryAssembly().FullProcessPath());
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

        public static string[] ParseCommandHostArgumentsFromCommandLineArguments(
            string[] commandLineArguments, 
            out bool forceConsoleHost,
            out bool forceNoninteractiveHost,
            out string monitorMutexHost)
        {
            // Sorry for the mess, we can't set the out param in a lambda
            var forceConsole = false;
            var optionSet = ConsoleHost.AddConsoleSwitch(new OptionSet(), v => forceConsole = true);
            string monitorMutex = null;
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

        static ICommandHost SelectMostAppropriateHost(ICommand command,
            string displayName,
            ILog log,
            bool forceConsoleHost,
            bool forceNoninteractiveHost,
            string monitorMutexHost)
        {
            log.Trace("Selecting the most appropriate host");

            var commandSupportsConsoleSwitch = ConsoleHost.HasConsoleSwitch(command.Options);

            if (!string.IsNullOrEmpty(monitorMutexHost))
            {
                log.Trace("The --monitorMutex switch was provided for a supported command");
                return new MutexHost(monitorMutexHost);
            }
        
            if (forceNoninteractiveHost && commandSupportsConsoleSwitch)
            {
                log.Trace($"The --noninteractive switch was provided for a supported command");
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
                return new WindowsServiceHost();
            }

            log.Trace("The program is running interactively; using a console host");
            return new ConsoleHost(displayName);
        }

        private static bool IsRunningAsAWindowsService(ILog log)
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
                }
                while (parentPid == 0 && Kernel32.Process32Next(hnd, ref processInfo));

                if (parentPid <= 0)
                    return false;

                var parent =  Process.GetProcessById(parentPid);
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

        private readonly object singleShutdownLock = new object();
        void Shutdown()
        {
            if (!Monitor.TryEnter(singleShutdownLock)) return;
            if (responsibleCommand != null)
            {
                log.TraceFormat("Sending stop signal to current command");
                responsibleCommand.Stop();
            }

            log.TraceFormat("Disposing of the container");
            container.Dispose();
        }

#pragma warning disable PC003 // Native API not available in UWP
#if USER_INTERACTIVE_DOES_NOT_WORK
        static class Kernel32
        {
            public static uint TH32CS_SNAPPROCESS = 2;

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESSENTRY32
            {
                public uint dwSize;
                public uint cntUsage;
                public uint th32ProcessID;
                public IntPtr th32DefaultHeapID;
                public uint th32ModuleID;
                public uint cntThreads;
                public uint th32ParentProcessID;
                public int pcPriClassBase;
                public uint dwFlags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
            };

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

            [DllImport("kernel32.dll")]
            public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

            [DllImport("kernel32.dll")]
            public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
        }
#endif
#pragma warning restore PC003 // Native API not available in UWP

#if FULL_FRAMEWORK
    public static class CtrlSignaling
    {
        [DllImport("Kernel32.dll")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
    
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);
    
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }
    }
#endif
    }
}