using System;

namespace Octopus.Tentacle.Communications
{
    public class ConsolePrompt : IPrompt
    {
        // When input has been redirected there is nobody at a keyboard to answer us, and reading from it would either
        // block forever or consume input intended for something else.
        public bool CanPrompt => !Console.IsInputRedirected;

        public bool Confirm(string context, string question)
        {
            Console.WriteLine();
            Console.WriteLine(context);
            Console.Write($"{question} [y/N]: ");

            var response = (Console.ReadLine() ?? string.Empty).Trim();

            return response.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                response.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
