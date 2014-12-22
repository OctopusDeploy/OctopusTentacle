using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Platform.Variables.Templates.Parser.Ast;

namespace Octopus.Platform.Variables.Templates.Parser
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
