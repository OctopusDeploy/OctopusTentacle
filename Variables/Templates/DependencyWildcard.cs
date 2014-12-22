using System;

namespace Octopus.Shared.Variables.Templates
{
    class DependencyWildcard : SymbolExpressionStep
    {
        public override string ToString()
        {
            return "*";
        }
    }
}