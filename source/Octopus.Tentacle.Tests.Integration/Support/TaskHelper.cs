using System;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public static class TaskHelper
    {
        public static Task<T> AsTask<T>(this T result)
        {
            return Task.FromResult(result);
        }
    }
}