using System;

namespace Octopus.Shared.Conventions
{
    public interface IConventionProcessor
    {
        void RunConventions(ConventionContext context);
    }
}