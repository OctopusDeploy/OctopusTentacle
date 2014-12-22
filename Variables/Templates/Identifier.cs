using System;

namespace Octopus.Shared.Variables.Templates
{
    class Identifier : SymbolExpressionStep
    {
        readonly string _text;

        public Identifier(string text)
        {
            _text = text;
        }

        public string Text
        {
            get { return _text; }
        }

        public override string ToString()
        {
            return Text;
        }
    }
}