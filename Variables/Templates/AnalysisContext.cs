using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Variables.Templates
{
    class AnalysisContext
    {
        readonly AnalysisContext parent;
        readonly Identifier identifier;
        readonly SymbolExpression expansion;

        public AnalysisContext() {}
            
        AnalysisContext(AnalysisContext parent, Identifier identifier, SymbolExpression expansion)
        {
            this.parent = parent;
            this.identifier = identifier;
            this.expansion = expansion;
        }

        public string Expand(SymbolExpression expression)
        {
            return new SymbolExpression(Expand(expression.Steps)).ToString();
        }

        public IEnumerable<SymbolExpressionStep> Expand(IEnumerable<SymbolExpressionStep> expression)
        {
            var nodes = expression;
            var first = nodes.FirstOrDefault() as Identifier;
            if (identifier != null && first != null && string.Compare(first.Text, identifier.Text, StringComparison.OrdinalIgnoreCase) == 0)
            {
                nodes = expansion.Steps.Concat(new [] { new DependencyWildcard() }).Concat(nodes.Skip(1));
            }

            if (parent != null)
                nodes = parent.Expand(nodes);

            return nodes;
        }

        public AnalysisContext BeginChild(Identifier ident, SymbolExpression expan)
        {
            return new AnalysisContext(this, ident, expan);
        }
    }
}