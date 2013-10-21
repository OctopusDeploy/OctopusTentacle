using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Octopus.Platform.Util;
using Octopus.Shared.Internals.Options;

namespace Octopus.Shared.Startup
{
    public class HelpCommand : ICommand
    {
        readonly ICommandLocator commands;
        
        public HelpCommand(ICommandLocator commands)
        {
            this.commands = commands;
        }

        public string CommandName { get; set; }

        public OptionSet Options
        {
            get { return new OptionSet().WithExtras(extra => CommandName = extra.FirstOrDefault()); }
        }

        public void WriteHelp(TextWriter writer)
        {
            
        }

        public void Start(string[] commandLineArguments, ICommandRuntime commandRuntime)
        {
            var executable = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().FullLocalPath());

            if (string.IsNullOrEmpty(CommandName))
            {
                PrintGeneralHelp(executable);
            }
            else
            {
                var command = commands.Find(CommandName);

                if (command == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Command '{0}' is not supported", CommandName);
                    Console.ResetColor();
                    PrintGeneralHelp(executable);
                }
                else
                {
                    PrintCommandHelp(executable, command.Value, command.Metadata);
                }
            }
        }

        public void Stop()
        {
        }

        void PrintCommandHelp(string executable, ICommand command, ICommandMetadata metadata)
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

            Console.WriteLine();
            Console.Write("Or use ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("help <command>");
            Console.ResetColor();
            Console.WriteLine(" for more details.");
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