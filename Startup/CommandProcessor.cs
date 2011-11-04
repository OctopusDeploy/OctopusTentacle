using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using log4net;

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
                var options = command.Options;
                options.Parse(args);

                command.Execute();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        static string GetFirstArgument(IEnumerable<string> args)
        {
            return (args.FirstOrDefault() ?? string.Empty).ToLowerInvariant().TrimStart('-', '/');
        }
    }
}