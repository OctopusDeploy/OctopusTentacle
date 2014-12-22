using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Octopus.Platform.Variables.Templates.Parser.Ast
{
    /// <summary>
    /// A value, identified using dotted/bracketed notation, e.g.:
    /// <code>Octopus.Action[Name].Foo</code>. This would classically
    /// be represented using nesting "property expressions" rather than a path, but in the
    /// current very simple language a path is more convenient to work with.
    /// </summary>
    class SymbolExpression : ContentExpression
    {
        readonly SymbolExpressionStep[] _steps;

        public SymbolExpression(IEnumerable<SymbolExpressionStep> steps)
        {
            _steps = steps.ToArray();
        }

        public SymbolExpressionStep[] Steps
        {
            get { return _steps; }
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            var identifierJoin = "";
            foreach (var step in Steps)
            {
                if (step is Identifier)
                    result.Append(identifierJoin);

                result.Append(step);

                identifierJoin = ".";
            }

            return result.ToString();
        }
    }
}
