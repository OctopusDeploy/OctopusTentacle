using System;

namespace Octopus.Core.Util
{
    public class Maybe<T>
    {
        Maybe()
        {
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
        public static bool None<T>(this Maybe<T> maybe) => maybe == null || maybe == Maybe<T>.None;
        public static bool Some<T>(this Maybe<T> maybe) => !None(maybe);
    }
}