using System;
using Sprache;

namespace Octopus.Platform.Variables.Templates.Parser.Ast
{
    interface IInputToken
    {
        Position InputPosition { get; set; }
    }
}