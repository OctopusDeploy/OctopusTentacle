using System;
using Sprache;

namespace Octopus.Shared.Variables.Templates
{
    interface IInputToken
    {
        Position InputPosition { get; set; }
    }
}