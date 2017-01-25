using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class HelpCommand : ICommand
    {
        readonly ICommandLocator commands;

        public HelpCommand(ICommandLocator commands)
        {
            this.commands = commands;
        }

        public void WriteHelp(TextWriter writer)
        {
        }

        public void Start(string[] commandLineArguments, ICommandRuntime commandRuntime, OptionSet commonOptions, string displayName, string version, string informationalVersion, string[] environmentInformation, string instanceName)
        {
            var executable = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().FullLocalPath());

            var commandName = commandLineArguments.Length > 0 ? commandLineArguments[0] : null;

            if (string.IsNullOrEmpty(commandName))
            {
                PrintGeneralHelp(executable);
            }
            else
            {
                var command = commands.Find(commandName);

                if (command == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Command '{0}' is not supported", commandName);
                    Console.ResetColor();
                    PrintGeneralHelp(executable);
                }
                else
                {
                    PrintCommandHelp(executable, command.Value, command.Metadata, commonOptions);
                }
            }
        }

        public void Stop()
        {
        }

        void PrintCommandHelp(string executable, ICommand command, CommandMetadata metadata, OptionSet commonOptions)
        {
            Console.ResetColor();
            Console.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(executable + " " + metadata.Name + " [<options>]");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Where [<options>] is any of: ");
            Console.WriteLine();

            command.WriteHelp(Console.Out);

            if (commonOptions.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Or one of the common options: ");
                Console.WriteLine();

                commonOptions.WriteOptionDescriptions(Console.Out);
            }
        }

        void PrintGeneralHelp(string executable)
        {
            Console.ResetColor();
            Console.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(executable + " <command> [<options>]");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Where <command> is one of: ");
            Console.WriteLine();

            foreach (var possible in commands.List().OrderBy(x => x.Name))
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("  " + possible.Name.PadRight(15, ' '));
                Console.ResetColor();
                Console.WriteLine("   " + possible.Description);
            }

            Console.WriteLine();
            Console.Write("Or use ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("help <command>");
            Console.ResetColor();
            Console.WriteLine(" for more details.");
        }
    }
}