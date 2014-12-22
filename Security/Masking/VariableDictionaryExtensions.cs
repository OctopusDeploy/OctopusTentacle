using System;
using System.Linq;
using Octopus.Shared.Variables;

namespace Octopus.Shared.Security.Masking
{
    public static class VariableDictionaryExtensions
    {
        public static IDisposable AddToMaskingContext(this VariableDictionary variables)
        {
            if (variables == null) throw new ArgumentNullException("variables");
            return MaskingContext.Add(new SensitiveDataMask(variables.Where(v => v.IsSensitive).Select(v => v.Value)));
        }
    }
}
