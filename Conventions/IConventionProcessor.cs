using System;

namespace Octopus.Shared.Conventions
{
    public interface IConventionProcessor
    {
        void RunConventions(IConventionContext context);
    }
}