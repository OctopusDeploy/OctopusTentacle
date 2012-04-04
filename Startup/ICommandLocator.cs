using System;

namespace Octopus.Shared.Startup
{
    public interface ICommandLocator
    {
        ICommandMetadata[] List();
        ICommand Find(string name);
    }
}