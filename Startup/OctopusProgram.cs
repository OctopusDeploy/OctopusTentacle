using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using Autofac;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Octopus.Shared.Internals.Options;

namespace Octopus.Shared.Startup
{
    public abstract class OctopusProgram
    {
        readonly ILog log = Log.Octopus();
        readonly string displayName;
        readonly OptionSet commonOptions;
        IContainer container;
        ICommand commandInstance;
        string[] commandLineArguments;
        bool forceConsole;
        bool showLogo = true;

        protected OctopusProgram(string displayName, string[] commandLineArguments)
        {
            this.commandLineArguments = commandLineArguments;
            this.displayName = displayName;
            commonOptions = new OptionSet();
            commonOptions.Add("console", "Don't attempt to run as a service, even if the user is non-interactive", v => forceConsole = true);
            commonOptions.Add("nologo", "Don't print title or version information", v => showLogo = false);
        }

        protected OptionSet CommonOptions { get { return commonOptions; } }

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
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                log.InfoFormat(args.Exception.UnpackFromContainers(), "Unhandled task exception occurred: {0}", args.Exception.GetErrorSummary());
                args.SetObserved();
            };
            
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = (Exception)args.ExceptionObject;
                log.FatalFormat(exception, "Unhandled AppDomain exception occurred: {0}", exception.Message);
            };

            int exitCode;
            try
            {
                commandLineArguments = ProcessCommonOptions();
                var host = SelectMostAppropriateHost();
                host.Run(Start, Stop);
                exitCode = Environment.ExitCode;
            }
            catch (ArgumentException ex)
            {
                log.Fatal(ex.Message);
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
                log.Fatal(ex);
                exitCode = 100;
            }

            return exitCode;
        }

        ICommandHost SelectMostAppropriateHost()
        {
            log.Trace("Selecting the most appropriate host");

            if (forceConsole)
            {
                log.Trace("The --console switch was passed; using a console host");
                return new ConsoleHost(displayName, showLogo);
            }

            if (Environment.UserInteractive)
            {
                log.Trace("The program is running interactively; using a console host");
                return new ConsoleHost(displayName, showLogo);
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
            log.Trace("Creating and configuring the Autofac container");
            container = BuildContainer();
            RegisterAdditionalModules();

            var commandLocator = container.Resolve<ICommandLocator>();

            var commandName = ExtractCommandName(ref commandLineArguments);
            
            log.TraceFormat("Finding the implementation for command: {0}", commandName);
            var command = commandLocator.Find(commandName);
            if (command == null)
            {
                command = commandLocator.Find("help");
                Environment.ExitCode = -1;
            }

            commandInstance = command.Value;
            
            log.TraceFormat("Using command: {0}", commandInstance.GetType().Name);
            commandInstance.Start(commandLineArguments, commandRuntime, CommonOptions);
        }

        protected abstract IContainer BuildContainer();

        void RegisterAdditionalModules()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new CommandModule());
            builder.Update(container);
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