using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Variables.Templates
{
    public class Template
    {
        readonly TemplateToken[] _tokens;

        public Template(IEnumerable<TemplateToken> tokens)
        {
            _tokens = tokens.ToArray();
        }

        public TemplateToken[] Tokens
        {
            get { return _tokens; }
        }

        public override string ToString()
        {
            return string.Join("", _tokens.Cast<object>());
        }
    }
}
