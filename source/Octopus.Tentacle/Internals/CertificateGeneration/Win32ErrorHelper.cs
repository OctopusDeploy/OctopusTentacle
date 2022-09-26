using System;
#if NETFRAMEWORK
using System;
using System.Runtime.InteropServices;

namespace Octopus.Tentacle.Internals.CertificateGeneration
{
    internal static class Win32ErrorHelper
    {
        internal static void ThrowExceptionIfGetLastErrorIsNotZero()
        {
            var win32ErrorCode = Marshal.GetLastWin32Error();
            if (0 != win32ErrorCode)
                Marshal.ThrowExceptionForHR(HResultFromWin32(win32ErrorCode));
        }

        private static int HResultFromWin32(int win32ErrorCode)
        {
            if (win32ErrorCode > 0)
                return (int)(((uint)win32ErrorCode & 0x0000FFFF) | 0x80070000U);
            return win32ErrorCode;
        }
    }
}
#endif