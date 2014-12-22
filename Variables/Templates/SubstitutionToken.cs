using System;

namespace Octopus.Shared.Variables.Templates
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