using System;

namespace Octopus.Shared.Variables.Templates
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