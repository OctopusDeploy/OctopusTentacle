using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class CommandProcessor : ICommandProcessor
    {
        readonly ICommandLocator commandLocator;
        readonly ILog log;

        public CommandProcessor(ICommandLocator commandLocator, ILog log)
        {
            this.commandLocator = commandLocator;
            this.log = log;
        }

        public void Process(string[] args)
        {
            var first = GetFirstArgument(args);

            var command =
                commandLocator.Find(first) ??
                commandLocator.Find("help");

            if (command == null)
            {
                throw new InvalidOperationException(string.Format("The command '{0}' is not supported and no help exists.", first));
            }

            args = args.Skip(1).ToArray();

            try
            {
                var options = command.Value.Options;
                options.Parse(args);

                command.Value.Execute();
            }
            catch (ArgumentException ex)
            {
                log.Error(ex.Message);
                Environment.ExitCode = -1;
            }
            catch (SecurityException ex)
            {
                log.Error("Security exception: " + ex.Message);
                log.Error("Please try re-running the command as an administrator from an elevated command prompt.");
                Environment.ExitCode = -42;
            }
            catch (ReflectionTypeLoadException ex)
            {
                log.Error(ex);

                foreach (var exSub in ex.LoaderExceptions)
                {
                    log.Error(exSub);
                    
                    if (exSub is FileNotFoundException)
                    {
                        var exFileNotFound = exSub as FileNotFoundException;
                        if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                        {
                            log.ErrorFormat("Fusion log: {0}", exFileNotFound.FusionLog);
                        }
                    }
                }
                Environment.ExitCode = -43;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                Environment.ExitCode = ex.GetType().Name.GetHashCode();
            }
        }

        static string GetFirstArgument(IEnumerable<string> args)
        {
            return (args.FirstOrDefault() ?? string.Empty).ToLowerInvariant().TrimStart('-', '/');
        }
    }
}