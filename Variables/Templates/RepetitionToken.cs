using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Variables.Templates
{
    class RepetitionToken : TemplateToken
    {
        readonly SymbolExpression _collection;
        readonly Identifier _enumerator;
        readonly TemplateToken[] _template;

        public RepetitionToken(SymbolExpression collection, Identifier enumerator, IEnumerable<TemplateToken> template)
        {
            _collection = collection;
            _enumerator = enumerator;
            _template = template.ToArray();
        }

        public SymbolExpression Collection
        {
            get { return _collection; }
        }

        public Identifier Enumerator
        {
            get { return _enumerator; }
        }

        public TemplateToken[] Template
        {
            get { return _template; }
        }

        public override string ToString()
        {
            return "#{each " + Enumerator + " in " + Collection + "}" + string.Join("", Template.Cast<object>()) + "#{/each}";
        }
    }
}