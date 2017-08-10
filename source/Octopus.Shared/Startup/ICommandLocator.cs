using System;

namespace Octopus.Shared.Startup
{
    public interface ICommandLocator
    {
        CommandMetadata[] List();
        Lazy<ICommand, CommandMetadata> Find(string name);
    }
}