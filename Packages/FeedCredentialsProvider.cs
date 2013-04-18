using System;
using System.Collections.Concurrent;
using System.Net;
using NuGet;

namespace Octopus.Shared.Packages
{
    public class FeedCredentialsProvider : ICredentialProvider
    {
        FeedCredentialsProvider()
        {
        }

        public static FeedCredentialsProvider Instance = new FeedCredentialsProvider();
        static readonly ConcurrentDictionary<string, ICredentials> Credentials = new ConcurrentDictionary<string, ICredentials>();
        static readonly ConcurrentDictionary<string, RetryTracker> Retries = new ConcurrentDictionary<string, RetryTracker>();

        public void SetCredentials(Uri uri, ICredentials credential)
        {
            Credentials[Canonicalize(uri)] = credential;
        }

        public ICredentials GetCredentials(Uri uri, IWebProxy proxy, CredentialType credentialType, bool retrying)
        {
            var url = Canonicalize(uri);
            var retry = Retries.GetOrAdd(url, _ => new RetryTracker());

            if (!retrying)
            {
                retry.Reset();
            }
            else
            {
                var retryAllowed = retry.AttemptRetry();
                if (!retryAllowed)
                    return null;
            }

            ICredentials credential;
            if (!Credentials.TryGetValue(url, out credential))
            {
                credential = CredentialCache.DefaultNetworkCredentials;
            }

            return credential;
        }

        string Canonicalize(Uri uri)
        {
            return uri.Authority.ToLowerInvariant().Trim();
        }

        public class RetryTracker
        {
            const int MaxAttempts = 3;
            int currentCount;

            public bool AttemptRetry()
            {
                if (currentCount > MaxAttempts) return false;

                currentCount++;
                return true;
            }

            public void Reset()
            {
                currentCount = 0;
            }
        }
    }
}