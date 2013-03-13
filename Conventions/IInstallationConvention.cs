using System;

namespace Octopus.Shared.Conventions
{
    public interface IInstallationConvention : IConvention
    {
        void Install(ConventionContext context);
    }
}