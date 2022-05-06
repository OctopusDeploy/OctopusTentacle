
#nullable disable
#if NETFRAMEWORK
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Octopus.Diagnostics;

namespace Octopus.Shared.Internals.CertificateGeneration
{
    // This class has just enough functionality for generating self-signed certs.
    // In the future, I may expand it to do other things.
    public class CryptContext : FinalizableObject
    {
        readonly ISystemLog log;
        IntPtr handle = IntPtr.Zero;

        /// <summary>
        /// By default, sets up to create a new randomly named key container
        /// </summary>
        public CryptContext(ISystemLog log)
        {
            this.log = log;
            ContainerName = Guid.NewGuid().ToString();
            ProviderType = 1; // default RSA provider
            Flags = 8; // create new keyset
        }

        public IntPtr Handle => handle;

        public string ContainerName { get; set; }
        public string ProviderName { get; set; }
        public int ProviderType { get; set; }
        public int Flags { get; set; }

        public void Open()
        {
            ThrowIfDisposed();
            if (!Win32Native.CryptAcquireContext(out handle,
                ContainerName,
                ProviderName,
                ProviderType,
                Flags))
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();
        }

        public X509Certificate2 CreateSelfSignedCertificate(SelfSignedCertProperties properties)
        {
            try
            {
                return CreateSha256SelfSignedCertificate(properties);
            }
            catch (COMException comException)
            {
                if (comException.Message == "Exception from HRESULT: 0xC0000005" && Is32BitWindows2008())
                {
                    log.Warn("Falling back to SHA1 certificate, as SHA256 certificates are not supported on this 32 bit Windows 2008 install");
                    return CreateSha1SelfSignedCertificate(properties);
                }

                throw;
            }
        }

        X509Certificate2 CreateSha256SelfSignedCertificate(SelfSignedCertProperties properties)
        {
            ThrowIfDisposedOrNotOpen();

            GenerateKeyExchangeKey(properties.IsPrivateKeyExportable, properties.KeyBitLength);

            var asnName = properties.Name.RawData;
            var asnNameHandle = GCHandle.Alloc(asnName, GCHandleType.Pinned);

            const string OID_RSA_SHA256RSA = "1.2.840.113549.1.1.11";

            var signatureAlgorithm = new Win32Native.CryptoAlgorithmIdentifier
            {
                pszObjId = OID_RSA_SHA256RSA
            };
            var kpi = new Win32Native.CryptKeyProviderInformation
            {
                ContainerName = ContainerName,
                KeySpec = (int)KeyType.Exchange,
                ProviderType = 24,
                ProviderName = "Microsoft Enhanced RSA and AES Cryptographic Provider"
            };

            var certContext = Win32Native.CertCreateSelfSignCertificate(
                handle,
                new Win32Native.CryptoApiBlob(asnName.Length, asnNameHandle.AddrOfPinnedObject()),
                0,
                kpi,
                signatureAlgorithm,
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

        X509Certificate2 CreateSha1SelfSignedCertificate(SelfSignedCertProperties properties)
        {
            ThrowIfDisposedOrNotOpen();

            GenerateKeyExchangeKey(properties.IsPrivateKeyExportable, properties.KeyBitLength);

            var asnName = properties.Name.RawData;
            var asnNameHandle = GCHandle.Alloc(asnName, GCHandleType.Pinned);

            var kpi = new Win32Native.CryptKeyProviderInformation
            {
                ContainerName = ContainerName,
                KeySpec = (int)KeyType.Exchange,
                ProviderType = 1
            };

            var certContext = Win32Native.CertCreateSelfSignCertificate_2008(
                handle,
                new Win32Native.CryptoApiBlob(asnName.Length, asnNameHandle.AddrOfPinnedObject()),
                0,
                kpi,
                IntPtr.Zero,
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

        static bool Is32BitWindows2008()
            => Environment.OSVersion.Platform == PlatformID.Win32NT &&
                Environment.OSVersion.Version.Major == 6 &&
                Environment.OSVersion.Version.Minor == 0 &&
                !Environment.Is64BitOperatingSystem;

        Win32Native.SystemTime ToSystemTime(DateTime dateTime)
        {
            var fileTime = dateTime.ToFileTime();
            var systemTime = new Win32Native.SystemTime();
            if (!Win32Native.FileTimeToSystemTime(ref fileTime, systemTime))
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();
            return systemTime;
        }

        KeyExchangeKey GenerateKeyExchangeKey(bool exportable, int keyBitLength)
        {
            ThrowIfDisposedOrNotOpen();

            var flags = (exportable ? 1U : 0U) | ((uint)keyBitLength << 16);

            IntPtr keyHandle;
            var result = Win32Native.CryptGenKey(handle, (int)KeyType.Exchange, flags, out keyHandle);
            if (!result)
                Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();

            return new KeyExchangeKey(this, keyHandle);
        }

        SignatureKey GenerateSignatureKey(bool exportable, int keyBitLength)
        {
            ThrowIfDisposedOrNotOpen();

            var flags = (exportable ? 1U : 0U) | ((uint)keyBitLength << 16);

            IntPtr keyHandle;
            var result = Win32Native.CryptGenKey(handle, (int)KeyType.Signature, flags, out keyHandle);
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
                if (!Win32Native.CryptReleaseContext(handle, 0))
                    // only throw exceptions if we're NOT in a finalizer
                    if (viaDispose)
                        Win32ErrorHelper.ThrowExceptionIfGetLastErrorIsNotZero();
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
#endif