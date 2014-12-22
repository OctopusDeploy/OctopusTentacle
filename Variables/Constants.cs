using System;

namespace Octopus.Platform.Variables
{
    public static class Constants
    {
        public static class Each
        {
            public static readonly string First = "Octopus.Template.Each.First";
            public static readonly string Last = "Octopus.Template.Each.Last";
        }

        public static readonly string IgnoreMissingVariableTokens = "OctopusIgnoreMissingVariableTokens";
        public static readonly string PrintVariables = "OctopusPrintVariables";

        public static bool IsBuiltInName(string name)
        {
            return name.StartsWith("Octopus.Template*");
        }
    }
}
