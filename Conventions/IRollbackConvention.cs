using System;

namespace Octopus.Shared.Conventions
{
    public interface IRollbackConvention : IConvention
    {
        void Rollback(IConventionContext context);

        void Cleanup(IConventionContext context);
    }
}