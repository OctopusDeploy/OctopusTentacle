using System;
using Sprache;

namespace Octopus.Shared.Variables.Templates
{
    public abstract class TemplateToken : IInputToken
    {
        public Position InputPosition { get; set; }
    }
}