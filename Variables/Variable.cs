using System;
using Octopus.Shared.Variables.Templates;

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

        public bool IsPrintable
        {
            get { return !Name.Contains("CustomScripts."); }
        }

        public bool HasReferences()
        {
            Template result;
            string parserError;
            return TemplateParser.TryParseTemplate(Value, out result, out parserError) &&
                (result.Tokens.Length > 1 ||
                 result.Tokens.Length == 1 && !(result.Tokens[0] is TextToken));
        }

        public override string ToString()
        {
            return string.Format("{0} = {1}", Name, IsSensitive ? "********" : Value);
        }

        public Variable Clone()
        {
            return new Variable(Name, Value, IsSensitive);
        }
    }
}