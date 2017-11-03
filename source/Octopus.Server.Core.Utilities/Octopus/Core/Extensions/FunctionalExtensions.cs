using System;

namespace Octopus.Core.Extensions
{
    /// <summary>
    /// https://davefancher.com/2015/06/14/functional-c-fluent-interfaces-and-functional-method-chaining/
    /// https://app.pluralsight.com/library/courses/functional-programming-csharp/table-of-contents
    /// 
    /// Function extension libraries from the course:
    /// Functional Programming with C# by Dave Fancher
    /// </summary>
    public static class FunctionalExtensions
    {
        public static T Tee<T>(this T @this, Action<T> action)
        {
            action(@this);
            return @this;
        }
        
        public static TResult Using<TDisposable, TResult>
        (
            Func<TDisposable> factory,
            Func<TDisposable, TResult> fn)
            where TDisposable : IDisposable
        {
            using (var disposable = factory())
            {
                return fn(disposable);
            }
        }
        
        public static TResult Map<TSource, TResult>(
            this TSource @this,
            Func<TSource, TResult> fn)
        {
            return fn(@this);
        }
    }
}