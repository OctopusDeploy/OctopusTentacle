#nullable disable
#if NETFRAMEWORK
using System;
using System.Runtime.InteropServices;

namespace Octopus.Shared.Internals.CertificateGeneration
{
    class Win32Native
    {
        [DllImport("AdvApi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptReleaseContext(IntPtr ctx, int flags);

        [DllImport("AdvApi32.dll",
            EntryPoint = "CryptAcquireContextW",
            ExactSpelling = true,
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptAcquireContext(
            out IntPtr providerContext,
            string containerName,
            string providerName,
            int providerType,
            int flags);

        [DllImport("AdvApi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptDestroyKey(IntPtr cryptKeyHandle);

        [DllImport("AdvApi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CryptGenKey(
            IntPtr providerContext,
            int algorithmId,
            uint flags,
            out IntPtr cryptKeyHandle);

        [DllImport("Crypt32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr CertCreateSelfSignCertificate(
            IntPtr providerHandle,
            [In]
            CryptoApiBlob subjectIssuerBlob,
            int flags,
            [In]
            CryptKeyProviderInformation keyProviderInfo,
            [In]
            CryptoAlgorithmIdentifier signatureAlgorithm,
            [In]
            SystemTime startTime,
            [In]
            SystemTime endTime,
            IntPtr extensions);

        [DllImport("Crypt32.dll", ExactSpelling = true, SetLastError = true, EntryPoint = "CertCreateSelfSignCertificate")]
        internal static extern IntPtr CertCreateSelfSignCertificate_2008(
            IntPtr providerHandle,
            [In]
            CryptoApiBlob subjectIssuerBlob,
            int flags,
            [In]
            CryptKeyProviderInformation keyProviderInfo,
            IntPtr signatureAlgorithm,
            [In]
            SystemTime startTime,
            [In]
            SystemTime endTime,
            IntPtr extensions);

        [DllImport("Crypt32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CertFreeCertificateContext(IntPtr certContext);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FileTimeToSystemTime(
            [In]
            ref long fileTime,
            [Out]
            SystemTime systemTime);

        #region Nested type: CryptKeyProviderInformation

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal class CryptKeyProviderInformation
        {
            public string ContainerName;
            public string ProviderName;
            public int ProviderType;
            public int Flags;
            public int ProviderParameterCount;
            public IntPtr ProviderParameters;
            public int KeySpec;
        }

        #endregion

        [StructLayout(LayoutKind.Sequential)]
        public class CryptoAlgorithmIdentifier
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pszObjId;

            public CryptoApiBlob parameters;
        }

        #region Nested type: CryptoApiBlob

        [StructLayout(LayoutKind.Sequential)]
        internal class CryptoApiBlob
        {
            public int DataLength;
            public IntPtr Data;

            public CryptoApiBlob(int dataLength, IntPtr data)
            {
                DataLength = dataLength;
                Data = data;
            }
        }

        #endregion

        #region Nested type: SystemTime

        [StructLayout(LayoutKind.Sequential)]
        internal class SystemTime
        {
            public short Year;
            public short Month;
            public short DayOfWeek;
            public short Day;
            public short Hour;
            public short Minute;
            public short Second;
            public short Milliseconds;
        }

        #endregion
    }
}
#endif