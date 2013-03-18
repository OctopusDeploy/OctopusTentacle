using System;

namespace Octopus.Shared.Integration.Transforms
{
    public enum TransformResult
    {
        Success,
        SuccessWithErrors,
        SuccessWithWarnings,
        Failed
    }
}
