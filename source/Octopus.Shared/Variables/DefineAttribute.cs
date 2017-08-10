using System;

namespace Octopus.Shared.Variables
{
    public class DefineAttribute : Attribute
    {
        public string Description { get; set; }
        public string Example { get; set; }
        public VariableCategory Category { get; set; }
        public string Pattern { get; set; }
        public VariableDomain Domain { get; set; }
    }
}