using System;
using Octopus.Platform.Deployment.Conventions;

namespace Octopus.Shared.Conventions
{
    public interface IConventionProcessor
    {
        void RunConventions(IConventionContext context);
    }
}