using System;

namespace Octopus.Shared.Scripts
{
    public class AcquireLockResult
    {
        public bool Acquired { get; }
        public IDisposable? LockReleaser { get; }

        AcquireLockResult(bool acquired, IDisposable? lockReleaser)
        {
            Acquired = acquired;
            LockReleaser = lockReleaser;
        }

        public static AcquireLockResult Success(IDisposable lockReleaser)
        {
            return new AcquireLockResult(true, lockReleaser);
        }

        public static AcquireLockResult Fail()
        {
            return new AcquireLockResult(false, null);
        }
    }
}