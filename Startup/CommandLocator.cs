using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Startup
{
    public class CommandLocator : ICommandLocator
    {
        readonly IEnumerable<Lazy<ICommand, ICommandMetadata>> commands;

        public CommandLocator(IEnumerable<Lazy<ICommand, ICommandMetadata>> commands)
        {
            this.commands = commands;
        }

        public ICommandMetadata[] List()
        {
            return commands.Select(x => x.Metadata).ToArray();
        }

        public ICommand Find(string name)
        {
            var matchingCommands =
                from command in commands
                let commandName = command.Metadata.Name
                let aliases = command.Metadata.Aliases
                where commandName == name || aliases.Any(a => a == name)
                select command.Value;

            return matchingCommands.FirstOrDefault();
        }
    }
}