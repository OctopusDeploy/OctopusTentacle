using System;

namespace Octopus.Shared.Conventions
{
    public interface IInstallationConvention : IConvention
    {
        void Install(IConventionContext context);
    }
}