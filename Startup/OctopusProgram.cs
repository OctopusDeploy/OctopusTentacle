using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using NLog;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Diagnostics.KnowledgeBase;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public abstract class OctopusProgram
    {
        readonly ILogWithContext log = Log.Octopus();
        readonly string displayName;
        readonly string version;
        readonly string informationalVersion;
        readonly OptionSet commonOptions;
        IContainer container;
        ICommand commandInstance;
        string[] commandLineArguments;
        bool forceConsole;
        bool showLogo = true;
        
        protected OctopusProgram(string displayName, string version, string informationalVersion, string[] commandLineArguments)
        {
            this.commandLineArguments = commandLineArguments;
            this.displayName = displayName;
            this.version = version;
            this.informationalVersion = informationalVersion;
            commonOptions = new OptionSet();
            commonOptions.Add("console", "Don't attempt to run as a service, even if the user is non-interactive", v => forceConsole = true);
            commonOptions.Add("nologo", "Don't print title or version information", v => showLogo = false);
            commonOptions.Add("noconsolelogging", "Don't log to the console", v =>
            {
                // suppress logging to the console
                var c = LogManager.Configuration;
                var target = c.FindTargetByName("console");
                foreach (var rule in c.LoggingRules)
                {
                    rule.Targets.Remove(target);
                }
                LogManager.Configuration = c;
            });
        }

        protected OptionSet CommonOptions
        {
            get { return commonOptions; }
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
            Log.Appenders.Add(new NLogAppender());
            
            log.Trace("OctopusProgram.Run() starting");
            log.Trace("OctopusProgram.Run() : adding handler for TaskScheduler.UnobservedTaskException");
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                if (Debugger.IsAttached) Debugger.Break();
                log.ErrorFormat(args.Exception.UnpackFromContainers(), "Unhandled task exception occurred: {0}", args.Exception.GetErrorSummary());
                args.SetObserved();
            };

            log.Trace("OctopusProgram.Run() : adding handler for AppDomain.CurrentDomain.UnhandledException");
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (Debugger.IsAttached) Debugger.Break();
                var exception = args.ExceptionObject as Exception; // May not actually be one.
                log.FatalFormat(exception, "Unhandled AppDomain exception occurred: {0}", exception == null ? args.ExceptionObject : exception.GetErrorSummary());
            };

            int exitCode;
            try
            {
                log.Trace("OctopusProgram.Run() : Processing command line arguments");

                commandLineArguments = ProcessCommonOptions();
                
                var instanceName = string.Empty;
                var options = new OptionSet();
                options.Add("instance=", "Name of the instance to use", v => instanceName = v);
                var parsedOptions = options.Parse(commandLineArguments);
                
                log.Trace("Creating and configuring the Autofac container");
                container = BuildContainer(instanceName);
                log.Trace("OctopusProgram.Start() : Registering additional modules");
                RegisterAdditionalModules(container);

                if (showLogo)
                {
                    log.Info($"{displayName} version {version} ({informationalVersion})");
                }

                var host = SelectMostAppropriateHost();
                log.Trace("OctopusProgram.Run() : Host is " + host.GetType());
                log.Trace("OctopusProgram.Run() : Running host");
                host.Run(Start, Stop);
                exitCode = Environment.ExitCode;
            }
            catch (ControlledFailureException ex)
            {
                log.Fatal(ex.Message);
                exitCode = 1;
            }
            catch (ArgumentException ex)
            {
                log.Fatal(ex.Message);
                exitCode = 1;
            }
            catch (DependencyResolutionException ex) when (ex.InnerException is ArgumentException)
            {
                log.Fatal(ex.InnerException.Message);
                exitCode = 1;
            }
            catch (SecurityException ex)
            {
                log.Fatal(ex, "A security exception was encountered. Please try re-running the command as an Administrator from an elevated command prompt.");
                log.Fatal(ex);
                exitCode = 42;
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

                exitCode = 43;
            }
            catch (Exception ex)
            {
                var unpacked = ex.UnpackFromContainers();
                log.Error(new string('=', 79));
                log.Fatal(unpacked);

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
                exitCode = 100;
            }
            if (exitCode != 0 && Debugger.IsAttached)
                Debugger.Break();
            return exitCode;
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
            log.Trace("Processing common command line options");
            return CommonOptions.Parse(commandLineArguments).ToArray();
        }

        void Start(ICommandRuntime commandRuntime)
        {
            log.Trace("OctopusProgram.Start() : Resolving command locator");
            var commandLocator = container.Resolve<ICommandLocator>();

            log.Trace("OctopusProgram.Start() : Extracting command name");
            var commandName = ExtractCommandName(ref commandLineArguments);

            log.TraceFormat("OctopusProgram.Start() : Finding the implementation for command: {0}", commandName);
            var command = commandLocator.Find(commandName);
            if (command == null)
            {
                command = commandLocator.Find("help");
                Environment.ExitCode = -1;
            }

            commandInstance = command.Value;

            log.TraceFormat("OctopusProgram.Start() : Starting command: {0}", commandInstance.GetType().Name);
            commandInstance.Start(commandLineArguments, commandRuntime, CommonOptions);
            log.Trace("OctopusProgram.Start() : Command starting command");
        }

        protected abstract IContainer BuildContainer(string instanceName);

        protected virtual void RegisterAdditionalModules(IContainer builtContainer)
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new CommandModule());
            builder.Update(builtContainer);
        }

        static string ExtractCommandName(ref string[] args)
        {
            var first = (args.FirstOrDefault() ?? string.Empty).ToLowerInvariant().TrimStart('-', '/');
            args = args.Skip(1).ToArray();
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