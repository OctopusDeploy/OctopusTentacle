using System;

namespace Octopus.Shared.Variables
{
    public class Variable
    {
        public Variable()
        {
        }

        public Variable(string name, string value, bool isSensitive = false)
        {
            Name = name;
            Value = value;
            IsSensitive = isSensitive;
        }

        public string Name { get; set; }
        public string Value { get; set; }
        public bool IsSensitive { get; set; }

        public override string ToString()
        {
            return string.Format("{0} = {1}", Name, IsSensitive ? "********" : Value);
        }
    }
}