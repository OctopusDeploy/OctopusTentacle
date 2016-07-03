using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Util
{
    public static class ResultExtensions
    {
        public static Result<TOut> Then<TIn, TOut>(
            this IReadOnlyCollection<Result<TIn>> results,
            Func<IEnumerable<TIn>, TOut> ifAllSuccessful
            )
        {
            if (results.All(r => r.WasSuccessful))
                return ifAllSuccessful(results.Select(r => r.Value));

            return Result<TOut>.Failed(results);
        }

        public static Result<TOut> Then<TIn, TOut>(
            this Result<TIn> result,
            Func<TIn, Result<TOut>> ifSuccessful
            )
        {
            return result.WasSuccessful ? ifSuccessful(result.Value) : Result<TOut>.Failed(result);
        }

        public static Result<TOut> Then<TIn, TOut>(
            this Result<TIn> result,
            Func<TIn, TOut> ifSuccessful
            )
        {
            return result.WasSuccessful ? Result<TOut>.Success(ifSuccessful(result.Value)) : Result<TOut>.Failed(result);
        }

        public static Result<IReadOnlyList<T>> InvertToList<T>(this IEnumerable<Result<T>> results)
        {
            IReadOnlyList<Result<T>> resultsArr = results.ToArray();
            if (resultsArr.All(r => r.WasSuccessful))
                return Result<IReadOnlyList<T>>.Success(resultsArr.Select(r => r.Value).ToArray());
            return Result<IReadOnlyList<T>>.Failed(resultsArr);
        }

        public static Result<TOut> Combine<TA, TB, TOut>(this Result<TA> a, Result<TB> b, Func<TA, TB, TOut> transform)
        {
            if (a.WasSuccessful && b.WasSuccessful)
                return transform(a.Value, b.Value);
            return Result<TOut>.Failed(a, b);
        }

        public static Result<T> If<T>(this Result<T> a, IResult b)
        {
            if (a.WasSuccessful && b.WasSuccessful)
                return a.Value;
            return Result<T>.Failed(a, b);
        }

        public static Result<T> If<T>(this IResult a, Result<T> b)
        {
            if (a.WasSuccessful && b.WasSuccessful)
                return b.Value;
            return Result<T>.Failed(a, b);
        }
    }
}