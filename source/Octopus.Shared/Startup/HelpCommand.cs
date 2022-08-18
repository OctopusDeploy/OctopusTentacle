using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Util;

namespace Octopus.Shared.Startup
{
    public class HelpCommand : AbstractCommand
    {
        static readonly string TextFormat = "text";
        static readonly string JsonFormat = "json";
        static readonly string[] SupportedFormats = { TextFormat, JsonFormat };

        readonly ICommandLocator commands;
        readonly ISystemLog log;

        public HelpCommand(ICommandLocator commands, ISystemLog log, ILogFileOnlyLogger logFileOnlyLogger) : base(logFileOnlyLogger)
        {
            this.commands = commands;
            this.log = log;

            Options.Add("format=", $"The format of the output ({string.Join(",", SupportedFormats)}). Defaults to {Format}.", v => Format = v);
        }

        public string Format { get; set; } = TextFormat;

        public override void Start(string[] commandLineArguments, ICommandRuntime commandRuntime, OptionSet commonOptions)
        {
            base.Start(commandLineArguments, commandRuntime, commonOptions);

            var processPath = Assembly.GetEntryAssembly()?.FullProcessPath() ?? throw new Exception("Could not get path of the current process");
            var executable = PlatformDetection.IsRunningOnWindows
                ? Path.GetFileNameWithoutExtension(processPath)
                : Path.GetFileName(processPath);

            var firstArgument = commandLineArguments.FirstOrDefault() ?? string.Empty;
            var commandName = LooksLikeCommand(firstArgument) ? firstArgument : null;

            if (string.IsNullOrWhiteSpace(commandName))
            {
                PrintGeneralHelp(executable);
            }
            else
            {
                var command = commands.Find(commandName);

                if (command == null)
                {
                    log.Error($"Command '{commandName}' is not supported");
                    Console.WriteLine($"See '{executable} help'");
                }
                else
                {
                    PrintCommandHelp(executable, command.Value, command.Metadata, commonOptions);
                }
            }
        }

        bool LooksLikeCommand(string candidate)
            => candidate.Length > 0 && char.IsLetter(candidate.First());

        protected override void UnrecognizedArguments(IList<string> arguments)
        {
            // Ignore - we're showing help!
        }

        protected override void Start()
        {
        }

        void PrintCommandHelp(string executable, ICommand command, CommandMetadata metadata, OptionSet commonOptions)
        {
            if (string.Equals(Format, JsonFormat, StringComparison.OrdinalIgnoreCase))
                PrintCommandHelpAsJson(command, metadata, commonOptions);
            else
                PrintCommandHelpAsText(executable, command, metadata, commonOptions);
        }

        void PrintCommandHelpAsJson(ICommand command, CommandMetadata metadata, OptionSet commonOptions)
        {
            Console.Write(JsonConvert.SerializeObject(new
                {
                    metadata.Name,
                    metadata.Description,
                    metadata.Aliases,
                    Options = command.Options.Where(o => !o.Hide)
                        .Select(o => new
                        {
                            Name = o.Names.First(),
                            o.Description
                        })
                        .ToArray(),
                    CommonOptions = commonOptions.Where(o => !o.Hide)
                        .Select(o => new
                        {
                            Name = o.Names.First(),
                            o.Description
                        })
                        .ToArray()
                },
                Formatting.Indented));
        }

        static void PrintCommandHelpAsText(string executable, ICommand command, CommandMetadata metadata, OptionSet commonOptions)
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
            if (string.Equals(Format, JsonFormat, StringComparison.OrdinalIgnoreCase))
                PrintGeneralHelpAsJson();
            else
                PrintGeneralHelpAsText(executable);
        }

        void PrintGeneralHelpAsJson()
        {
            Console.Write(JsonConvert.SerializeObject(new
                {
                    Commands = commands.List()
                        .OrderBy(x => x.Name)
                        .Select(x => new
                        {
                            x.Name,
                            x.Description,
                            x.Aliases
                        })
                },
                Formatting.Indented));
        }

        void PrintGeneralHelpAsText(string executable)
        {
            Console.ResetColor();
            Console.Write("Usage: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(executable + " <command> [<options>]");
            Console.ResetColor();
            Console.WriteLine();
            Console.Write("Where ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("<command>");
            Console.ResetColor();
            Console.WriteLine(" is one of: ");
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
            Console.Write("<command> --help");
            Console.ResetColor();
            Console.WriteLine(" for more details.");
        }
    }
}