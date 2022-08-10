using System;

namespace Octopus.Tentacle.Startup
{
    public interface ICommandLocator
    {
        CommandMetadata[] List();
        Lazy<ICommand, CommandMetadata>? Find(string name);
    }
}