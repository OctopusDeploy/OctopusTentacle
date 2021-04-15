using System;

namespace Octopus.CoreUtilities
{
    public class Maybe<T>
    {
        Maybe()
        {
            value = default!;
        }

        public static readonly Maybe<T> None = new Maybe<T>();

        public static Maybe<T> Some(T value)
        {
            return new Maybe<T>()
            {
                value = value
            };
        }

        T value;


        public T Value
        {
            get
            {
                if (this == None)
                    throw new InvalidOperationException("No value, check None() or Some() before accessing Value");
                return value;
            }
        }

    }

    public static class MaybeExtentions
    {
        public static Maybe<T> AsSome<T>(this T value) => Maybe<T>.Some(value);
        public static bool None<T>(this Maybe<T>? maybe) => maybe == null || maybe == Maybe<T>.None;
        public static bool Some<T>(this Maybe<T> maybe) => !None(maybe);

        public static T? SomeOrDefault<T>(this Maybe<T> maybe)
            => maybe.Some() ? maybe.Value : default(T);

        public static T SomeOr<T>(this Maybe<T> maybe, T ifNone)
            => maybe.Some() ? maybe.Value : ifNone;

        public static Maybe<R> Select<T, R>(this Maybe<T> maybe, Func<T, R> selector)
            => maybe.Some() ? selector(maybe.Value).AsSome() : Maybe<R>.None;

        public static R? SelectValueOrDefault<T, R>(this Maybe<T> maybe, Func<T, R> selector)
            => maybe.Some() ? selector(maybe.Value) : default(R);

        public static R SelectValueOr<T, R>(this Maybe<T> maybe, Func<T, R> selector, R ifNone)
            => maybe.Some() ? selector(maybe.Value) : ifNone;

        public static Maybe<T> ToMaybe<T>(this T? value) where T : class
            => value == null ? Maybe<T>.None : value.AsSome();
    }
}