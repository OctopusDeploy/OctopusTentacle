using System;

namespace Octopus.Tentacle.Util
{
    public class DeletionOptions : IEquatable<DeletionOptions>
    {
        DeletionOptions()
        {
            SleepBetweenAttemptsMilliseconds = 100;
        }

        public static DeletionOptions TryThreeTimes => new DeletionOptions { RetryAttempts = 3, ThrowOnFailure = true };

        public static DeletionOptions TryThreeTimesIgnoreFailure => new DeletionOptions { RetryAttempts = 3, ThrowOnFailure = false };

        public int RetryAttempts { get; private set; }
        public int SleepBetweenAttemptsMilliseconds { get; }
        public bool ThrowOnFailure { get; private set; }

        public bool Equals(DeletionOptions? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return RetryAttempts == other.RetryAttempts && SleepBetweenAttemptsMilliseconds == other.SleepBetweenAttemptsMilliseconds && ThrowOnFailure.Equals(other.ThrowOnFailure);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DeletionOptions)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RetryAttempts;
                hashCode = (hashCode * 397) ^ SleepBetweenAttemptsMilliseconds;
                hashCode = (hashCode * 397) ^ ThrowOnFailure.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(DeletionOptions left, DeletionOptions right)
            => Equals(left, right);

        public static bool operator !=(DeletionOptions left, DeletionOptions right)
            => !Equals(left, right);
    }
}