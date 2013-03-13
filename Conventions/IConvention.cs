using System;

namespace Octopus.Shared.Conventions
{
    public interface IConvention
    {
        int Priority { get; }
        string FriendlyName { get; }
    }
}