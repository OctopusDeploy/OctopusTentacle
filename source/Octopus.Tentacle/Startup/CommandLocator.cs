using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Tentacle.Startup
{
    public class CommandLocator : ICommandLocator
    {
        private readonly IEnumerable<Lazy<ICommand, CommandMetadata>> commands;

        public CommandLocator(IEnumerable<Lazy<ICommand, CommandMetadata>> commands)
        {
            this.commands = commands;
        }

        public CommandMetadata[] List()
        {
            return commands.Select(x => x.Metadata).ToArray();
        }

        public Lazy<ICommand, CommandMetadata>? Find(string name)
        {
            var matchingCommands =
                from command in commands
                let commandName = command.Metadata.Name
                let aliases = command.Metadata.Aliases
                where commandName == name || aliases.Any(a => a == name)
                select command;

            return matchingCommands.FirstOrDefault();
        }
    }
}