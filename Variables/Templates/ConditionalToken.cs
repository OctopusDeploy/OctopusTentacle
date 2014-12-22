using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Variables.Templates
{
    /// <summary>
    /// Example: <code>#{if Octopus.IsCool}...#{/if}</code>
    /// </summary>
    class ConditionalToken : TemplateToken
    {
        readonly SymbolExpression _expression;
        readonly TemplateToken[] _truthyTemplate;
        readonly TemplateToken[] _falsyTemplate;

        public ConditionalToken(SymbolExpression expression, IEnumerable<TemplateToken> truthyBranch, IEnumerable<TemplateToken> falsyBranch)
        {
            _expression = expression;
            _truthyTemplate = truthyBranch.ToArray();
            _falsyTemplate = falsyBranch.ToArray();
        }

        public SymbolExpression Expression
        {
            get { return _expression; }
        }

        public TemplateToken[] TruthyTemplate
        {
            get { return _truthyTemplate; }
        }

        public TemplateToken[] FalsyTemplate
        {
            get { return _falsyTemplate; }
        }

        public override string ToString()
        {
            return "#{if " + Expression + "}" + string.Join("", TruthyTemplate.Cast<object>()) + "#{else}" + string.Join("", FalsyTemplate.Cast<object>()) + "#{/if}";
        }
    }
}