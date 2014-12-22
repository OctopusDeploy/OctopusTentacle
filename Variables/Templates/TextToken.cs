using System;

namespace Octopus.Platform.Variables.Templates.Parser.Ast
{
    class TextToken : TemplateToken
    {
        readonly string _text;

        public TextToken(string text)
        {
            _text = text;
        }

        public string Text
        {
            get { return _text; }
        }

        public override string ToString()
        {
            return Text.Replace("#{", "##{");
        }
    }
}