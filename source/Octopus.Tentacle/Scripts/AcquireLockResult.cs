﻿using System;

namespace Octopus.Tentacle.Scripts
{
    public class AcquireLockResult
    {
        private AcquireLockResult(bool acquired, IDisposable? lockReleaser)
        {
            Acquired = acquired;
            LockReleaser = lockReleaser;
        }

        public bool Acquired { get; }
        public IDisposable? LockReleaser { get; }

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