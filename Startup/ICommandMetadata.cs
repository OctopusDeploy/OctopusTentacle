using System;

namespace Octopus.Shared.Startup
{
    public interface ICommandMetadata
    {
        string Name { get; }
        string[] Aliases { get; }
        string Description { get; }
    }
}