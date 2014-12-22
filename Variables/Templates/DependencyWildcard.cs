using System;

namespace Octopus.Platform.Variables.Templates.Parser.Ast
{
    class DependencyWildcard : SymbolExpressionStep
    {
        public override string ToString()
        {
            return "*";
        }
    }
}