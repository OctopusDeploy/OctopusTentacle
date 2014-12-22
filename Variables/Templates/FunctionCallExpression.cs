using System;
using System.Linq;

namespace Octopus.Platform.Variables.Templates.Parser.Ast
{
    /// <summary>
    /// Syntactically this appears as the <code>| FilterName</code> construct, where
    /// the (single) argument is specified to the left of the bar. Under the hood this
    /// same AST node will also represent classic <code>Function(Foo,Bar)</code> expressions.
    /// </summary>
    class FunctionCallExpression : ContentExpression
    {
        readonly bool filterSyntax;
        readonly string function;
        readonly ContentExpression[] arguments;

        public FunctionCallExpression(bool filterSyntax, string function, params ContentExpression[] arguments)
        {
            this.filterSyntax = filterSyntax;
            this.function = function;
            this.arguments = arguments;
        }

        public string Function
        {
            get { return function; }
        }

        public ContentExpression[] Arguments
        {
            get { return arguments; }
        }

        public override string ToString()
        {
            if (filterSyntax)
                return arguments[0] + " | "  + function;

            return function + "(" + string.Join(",", Arguments.Select(a => a.ToString())) + ")";
        }
    }
}
