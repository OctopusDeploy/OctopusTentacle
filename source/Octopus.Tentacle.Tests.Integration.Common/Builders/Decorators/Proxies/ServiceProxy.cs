﻿using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators.Proxies
{
    public abstract class ServiceProxy : DispatchProxyAsync
    {
        protected object? TargetService { get; private set; }

        protected void SetTargetService(object service)
        {
            TargetService = service;
        }

        public override object Invoke(MethodInfo method, object[] args)
            => throw new InvalidOperationException("We do not support decorating synchronous service methods.");

        public override async Task InvokeAsync(MethodInfo method, object[] args)
        {
            EnsureTargetServiceNotNull();
            try
            {
                //currently all of our service methods have either zero or one arg
                var request = args.FirstOrDefault();
                await OnStartingInvocationAsync(method, request).ConfigureAwait(false);

                var task = (Task)method.Invoke(TargetService, args);

                await task!.ConfigureAwait(false);
            }
            catch (TargetInvocationException e)
            {
                OnInvocationException(method, e.InnerException!);

                //we need to unwrap the TargetInvocationException
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
            catch (Exception e)
            {
                OnInvocationException(method, e);
                throw;
            }
            finally
            {
                await OnCompletingInvocationAsync(method, null).ConfigureAwait(false);
            }
        }

        public override async Task<T> InvokeAsyncT<T>(MethodInfo method, object[] args)
        {
            EnsureTargetServiceNotNull();
            T result = default;
            try
            {
                //currently all of our service methods have either zero or one arg
                var request = args.FirstOrDefault();
                await OnStartingInvocationAsync(method, request).ConfigureAwait(false);

                var task = (Task<T>)method.Invoke(TargetService, args);

                result = await task!.ConfigureAwait(false);

                return result;
            }
            catch (TargetInvocationException e)
            {
                OnInvocationException(method, e.InnerException!);

                //we need to unwrap the TargetInvocationException
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();

                //this will never be hit because the line above will throw an exception
                return default;
            }
            catch (Exception e)
            {
                OnInvocationException(method, e);
                throw;
            }
            finally
            {
                await OnCompletingInvocationAsync(method, result).ConfigureAwait(false);
            }
        }

        void EnsureTargetServiceNotNull()
        {
            if (TargetService is null)
                throw new InvalidOperationException("TargetService has not been set via SetTargetService().");
        }

        protected abstract Task OnStartingInvocationAsync(MethodInfo targetMethod, object? request);
        protected abstract Task OnCompletingInvocationAsync(MethodInfo targetMethod, object? response);
        protected abstract void OnInvocationException(MethodInfo targetMethod, Exception exception);
    }
}