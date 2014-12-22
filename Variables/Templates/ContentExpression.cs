using System;
using Sprache;

namespace Octopus.Shared.Variables.Templates
{
    /// <summary>
    /// The top-level "thing that has a textual value" that
    /// can be manipulated or inserted into the output.
    /// </summary>
    public abstract class ContentExpression : IInputToken
    {
        public Position InputPosition { get; set; }
    }
}