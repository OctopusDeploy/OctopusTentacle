using System;
using Halibut;

namespace Octopus.Tentacle.Client.Decorators
{
    public static class HalibutExceptionExtensions
    {
        public static bool IsHalibutOperationCancellationException(this Exception exception)
        {
            return exception is HalibutClientException && exception.Message.Contains("The operation was canceled");
        }
    }
}