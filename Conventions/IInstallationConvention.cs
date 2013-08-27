using System;
using Octopus.Platform.Deployment.Conventions;

namespace Octopus.Shared.Conventions
{
    public interface IInstallationConvention : IConvention
    {
        void Install(IConventionContext context);
    }
}