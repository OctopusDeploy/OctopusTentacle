using System;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Decorators
{
    public abstract class HalibutExceptionTentacleServiceDecorator
    {
        protected static Task<TResponse> HandleCancellationException<TResponse>(Func<Task<TResponse>> action)

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

        protected static Task HandleCancellationException(Func<Task> action)
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

        static OperationCanceledException CreateOperationCanceledException(Exception e) => throw new OperationCanceledException("The operation was cancelled", e);
    }
}