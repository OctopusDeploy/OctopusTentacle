using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Shared.Util
{
    public interface IResult
    {
        bool WasSuccessful { get; }
        string[] Errors { get; }
        string ErrorString { get; }
        bool WasFailure { get; }
    }

    public class Result : IResult
    {
        private string[] _errors;

        private Result()
        {

        }

        public bool WasSuccessful { get; private set; }
        public bool WasFailure => !WasSuccessful;

        public string[] Errors => _errors.ToArray();

        public string ErrorString => string.Join(Environment.NewLine, _errors);


        public static Result Failed(params string[] errors)
        {
            return new Result() { _errors = errors.ToArray() };
        }
        public static Result Failed(IReadOnlyList<string> errors)
        {
            return new Result() { _errors = errors.ToArray() };
        }

        public static Result Failed(params IResult[] becauseOf)
        {
            return new Result() { _errors = becauseOf.SelectMany(b => b.Errors).ToArray() };
        }

        public static Result Success()
        {
            return new Result() { WasSuccessful = true };
        }

        public static Result From(params Result[] results)
        {
            var failed = results.Where(r => !r.WasSuccessful).ToArray();
            if (failed.Length == 0)
                return Result.Success();
            return Result.Failed(failed.SelectMany(f => f.Errors).ToArray());
        }
    }

    public class Result<T> : IResult
    {
        private T _value;
        private string[] _errors;

        private Result()
        {

        }

        public bool WasSuccessful { get; private set; }

        public string[] Errors => _errors?.ToArray();

        public string ErrorString => _errors != null ? string.Join(Environment.NewLine, _errors) : null;

        public T Value
        {
            get
            {
                if (!WasSuccessful)
                    throw new Exception("No value as the operation was not successful");
                return _value;
            }
        }

        public bool WasFailure => !WasSuccessful;

        public static Result<T> Failed()
        {
            return new Result<T>() { _errors = new string[0] };
        }

        public static Result<T> Failed(params string[] errors)
        {
            return new Result<T>() { _errors = errors.ToArray() };
        }

        public static Result<T> Failed(params IResult[] becauseOf)
        {
            return new Result<T>() { _errors = becauseOf.Where(s => s.WasFailure).SelectMany(b => b.Errors).ToArray() };
        }

        public static Result<T> Failed(IReadOnlyCollection<IResult> becauseOf)
        {
            return new Result<T>() { _errors = becauseOf.Where(s => s.WasFailure).SelectMany(b => b.Errors).ToArray() };
        }

        public static Result<T> Success(T value)
        {
            return new Result<T>() { WasSuccessful = true, _value = value };
        }

        public static implicit operator Result<T>(T value)
        {
            return Success(value);
        }

        public static implicit operator T(Result<T> result)
        {
            return result.Value;
        }

        public T ValueOr(T def)
        {
            return WasSuccessful ? Value : def;
        }

        public override string ToString()
        {
            return WasSuccessful
                ? "" + Value
                : ErrorString;
        }

        public static Result<T> From<TIn>(Result<TIn> result) where TIn : T
        {
            return result.WasSuccessful
                ? Success(result.Value)
                : Failed(result);
        }
    }
}