using System;

namespace Octopus.Platform.Variables.Templates.Parser.Ast
{
    /// <summary>
    /// Example: <code>#{Octopus.Action[Foo].Name</code>.
    /// </summary>
    class SubstitutionToken : TemplateToken
    {
        readonly ContentExpression _expression;

        public SubstitutionToken(ContentExpression expression)
        {
            _expression = expression;
        }

        public ContentExpression Expression
        {
            get { return _expression; }
        }

        public override string ToString()
        {
            return "#{" + Expression + "}";
        }
    }
}