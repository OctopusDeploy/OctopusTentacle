
using System;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Tests.Support
{
    public static class TaskHelper
    {
        public static Task<T> AsTask<T>(this T result)
        {
            return Task.FromResult(result);
        }
    }
}