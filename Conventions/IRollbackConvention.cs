using System;

namespace Octopus.Shared.Conventions
{
    public interface IRollbackConvention : IConvention
    {
        void Rollback(ConventionContext context);

        void Cleanup(ConventionContext context);
    }
}