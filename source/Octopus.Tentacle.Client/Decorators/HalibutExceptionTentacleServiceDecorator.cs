using System;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Decorators
{
    public abstract class HalibutExceptionTentacleServiceDecorator
    {
        protected static async Task<TResponse> HandleCancellationException<TResponse>(Func<Task<TResponse>> action)
        {
            try
            {
                return await action();
            }
            catch (Exception e) when (e.IsHalibutOperationCancellationException())
            {
                throw CreateOperationCanceledException(e);
            }
        }

        protected static async Task HandleCancellationException(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception e) when (e.IsHalibutOperationCancellationException())
            {
                throw CreateOperationCanceledException(e);
            }
        }

        protected static TResponse HandleCancellationException<TResponse>(Func<TResponse> action)
        {
            try
            {
                return action();
            }
            catch (Exception e) when (e.IsHalibutOperationCancellationException())
            {
                throw CreateOperationCanceledException(e);
            }
        }

        protected static void HandleCancellationException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e) when (e.IsHalibutOperationCancellationException())
            {
                throw CreateOperationCanceledException(e);
            }
        }

        static OperationCanceledException CreateOperationCanceledException(Exception e) => new("The operation was cancelled", e);
    }
}