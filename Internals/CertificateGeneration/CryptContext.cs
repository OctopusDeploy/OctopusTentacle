using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Octopus.Shared.Internals.CertificateGeneration
{
    // This class has just enough functionality for generating self-signed certs.
    // In the future, I may expand it to do other things.
    public class CryptContext : FinalizableObject
    {
        IntPtr handle = IntPtr.Zero;

        /// <summary>
        /// By default, sets up to create a new randomly named key container
        /// </summary>
        public CryptContext()
        {
            ContainerName = Guid.NewGuid().ToString();
            ProviderType = 1; // default RSA provider
            Flags = 8; // create new keyset
        }

        public IntPtr Handle
        {
            get { return handle; }
        }

        public string ContainerName { get; set; }
        public string ProviderName { get; set; }
        public int ProviderType { get; set; }
        public int Flags { get; set; }

        public void Open()
        {
            ThrowIfDisposed();
            if (!Win32Native.CryptAcquireContext(out handle, ContainerName, ProviderName, ProviderType, Flags))
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();
        }

        public X509Certificate2 CreateSelfSignedCertificate(SelfSignedCertProperties properties)
        {
            ThrowIfDisposedOrNotOpen();

            GenerateKeyExchangeKey(properties.IsPrivateKeyExportable, properties.KeyBitLength);
            //GenerateSignatureKey(properties.IsPrivateKeyExportable, properties.KeyBitLength);

            var asnName = properties.Name.RawData;
            var asnNameHandle = GCHandle.Alloc(asnName, GCHandleType.Pinned);

            var kpi = new Win32Native.CryptKeyProviderInformation
                          {
                              ContainerName = ContainerName,
                              KeySpec = (int) KeyType.Exchange,
                              ProviderType = 1
                              // default RSA provider
                          };

            var certContext = Win32Native.CertCreateSelfSignCertificate(
                handle,
                new Win32Native.CryptoApiBlob(asnName.Length, asnNameHandle.AddrOfPinnedObject()),
                0, kpi, IntPtr.Zero,
                ToSystemTime(properties.ValidFrom),
                ToSystemTime(properties.ValidTo),
                IntPtr.Zero);

            asnNameHandle.Free();

            if (IntPtr.Zero == certContext)
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();

            var cert = new X509Certificate2(certContext); // dups the context (increasing it's refcount)

            if (!Win32Native.CertFreeCertificateContext(certContext))
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();

            return cert;
        }

        Win32Native.SystemTime ToSystemTime(DateTime dateTime)
        {
            var fileTime = dateTime.ToFileTime();
            var systemTime = new Win32Native.SystemTime();
            if (!Win32Native.FileTimeToSystemTime(ref fileTime, systemTime))
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();
            return systemTime;
        }

        public KeyExchangeKey GenerateKeyExchangeKey(bool exportable, int keyBitLength)
        {
            ThrowIfDisposedOrNotOpen();

            var flags = (exportable ? 1U : 0U) | ((uint) keyBitLength) << 16;

            IntPtr keyHandle;
            var result = Win32Native.CryptGenKey(handle, (int) KeyType.Exchange, flags, out keyHandle);
            if (!result)
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();

            return new KeyExchangeKey(this, keyHandle);
        }

        SignatureKey GenerateSignatureKey(bool exportable, int keyBitLength)
        {
            ThrowIfDisposedOrNotOpen();

            var flags = (exportable ? 1U : 0U) | ((uint) keyBitLength) << 16;

            IntPtr keyHandle;
            var result = Win32Native.CryptGenKey(handle, (int) KeyType.Signature, flags, out keyHandle);
            if (!result)
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();

            return new SignatureKey(this, keyHandle);
        }

        internal void DestroyKey(CryptKey key)
        {
            ThrowIfDisposedOrNotOpen();
            if (!Win32Native.CryptDestroyKey(key.Handle))
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();
        }

        protected override void CleanUp(bool viaDispose)
        {
            if (handle != IntPtr.Zero)
            {
                if (!Win32Native.CryptReleaseContext(handle, 0))
                {
                    // only throw exceptions if we're NOT in a finalizer
                    if (viaDispose)
                        Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();
                }
            }
        }

        void ThrowIfDisposedOrNotOpen()
        {
            ThrowIfDisposed();
            ThrowIfNotOpen();
        }

        void ThrowIfNotOpen()
        {
            if (IntPtr.Zero == handle)
                throw new InvalidOperationException("You must call CryptContext.Open first.");
        }
    }
}