using System;

namespace Octopus.Shared.Startup
{
    public interface ICommandLocator
    {
        ICommandMetadata[] List();
        Lazy<ICommand, ICommandMetadata> Find(string name);
    }
}