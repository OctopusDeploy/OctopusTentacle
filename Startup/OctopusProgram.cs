using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using Autofac;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    public abstract class OctopusProgram
    {
        readonly ILog log = Log.Octopus();
        readonly string[] commandLineArguments;
        readonly string displayName;
        readonly OptionSet commonOptions;
        IContainer container;
        ICommand commandInstance;

        protected OctopusProgram(string displayName, string[] commandLineArguments)
        {
            this.commandLineArguments = commandLineArguments;
            this.displayName = displayName;
            commonOptions = new OptionSet();
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
            int exitCode;
            try
            {
                var host = SelectMostAppropriateHost();
                host.Run(Start, Stop);
                exitCode = Environment.ExitCode;
            }
            catch (ArgumentException ex)
            {
                log.Error(ex.Message);
                exitCode = 1;
            }
            catch (SecurityException ex)
            {
                log.Error(ex, "A security exception was encountered. Please try re-running the command as an Administrator from an elevated command prompt.");
                log.Error(ex);
                exitCode = 42;
            }
            catch (ReflectionTypeLoadException ex)
            {
                log.Error(ex);

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
                log.Error(ex);
                exitCode = 100;
            }

            return exitCode;
        }

        ICommandHost SelectMostAppropriateHost()
        {
            log.Trace("Selecting the most appropriate host");

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
            var args = ProcessCommonOptions();

            log.Trace("Creating and configuring the Autofac container");
            container = BuildContainer();
            RegisterAdditionalModules();

            var commandLocator = container.Resolve<ICommandLocator>();

            var commandName = ExtractCommandName(ref args);
            
            log.TraceFormat("Finding the implementation for command: {0}", commandName);
            var command =
                commandLocator.Find(commandName) ??
                commandLocator.Find("help");

            commandInstance = command.Value;
            
            log.TraceFormat("Using command: {0}", commandInstance.GetType().Name);
            log.TraceFormat("Forwarding remaining command line options");
            commandInstance.Options.Parse(args);

            log.TraceFormat("Delegating to command");
            commandInstance.Start(commandRuntime);
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
            if (commandInstance == null) 
                return;

            log.TraceFormat("Sending stop signal to current command");
            commandInstance.Stop();
        }
    }
}