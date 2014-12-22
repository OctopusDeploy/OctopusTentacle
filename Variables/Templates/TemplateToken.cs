using System;
using Sprache;

namespace Octopus.Platform.Variables.Templates.Parser.Ast
{
    public abstract class TemplateToken : IInputToken
    {
        public Position InputPosition { get; set; }
    }
}