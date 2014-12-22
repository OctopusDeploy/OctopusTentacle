using System;
using Sprache;

namespace Octopus.Shared.Variables.Templates
{
    /// <summary>
    /// A segment of a <see cref="SymbolExpression"/>,
    /// e.g. <code>Octopus</code>, <code>[Foo]</code>.
    /// </summary>
    abstract class SymbolExpressionStep : IInputToken
    {
        public Position InputPosition { get; set; }
    }
}