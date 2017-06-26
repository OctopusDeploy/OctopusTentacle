using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using Octopus.Diagnostics;

namespace Octopus.Shared.Security.Certificates
{
    public static class CertificateEncoder
    {
        static readonly ILog Log = Diagnostics.Log.System();

        public static X509Certificate2 FromPfxFile(string pfxFilePath, string password)
        {
            X509Certificate2Collection certificates = new X509Certificate2Collection();
            if (string.IsNullOrEmpty(password))
            {
                Log.Info($"Importing the certificate stored in PFX file in {pfxFilePath}...");
                certificates.Import(pfxFilePath, string.Empty, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            }
            else
            {
                Log.Info($"Importing the certificate stored in PFX file in {pfxFilePath} using the provided password...");
                certificates.Import(pfxFilePath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            }

            if (certificates.Count == 0)
                throw new Exception($"The PFX file ({pfxFilePath}) does not contain any certificates.");

            if (certificates.Count > 1)
                Log.Info($"PFX file {pfxFilePath} contains multiple certificates, taking the first one.");

            var x509Certificate = certificates[0];

            if (CheckThatCertificateWasLoadedWithPrivateKey(x509Certificate) == false)
            {
                throw new CryptographicException("Unable to load X509 Certificate file. The X509 certificate file you provided does not include the private key. Please make sure the private key is included in your X509 certificate file and try again.");
            }

            return x509Certificate;
        }

        public static X509Certificate2 FromBase64String(string thumbprint, string certificateString)
        {
            if (certificateString == null) throw new ArgumentNullException("certificateString");
            return DoFromBase64String(thumbprint, certificateString, new X509Store("Octopus", StoreLocation.CurrentUser));
        }

        public static X509Certificate2 FromBase64String(string thumbprint, string certificateString, string password)
        {
            if (certificateString == null) throw new ArgumentNullException(nameof(certificateString));
            return DoFromBase64String(thumbprint, certificateString, password, new X509Store("Octopus", StoreLocation.CurrentUser));
        }

        public static X509Certificate2 FromBase64String(string thumbprint, string certificateString, StoreName storeName)
        {
            if (certificateString == null) throw new ArgumentNullException("certificateString");
            return DoFromBase64String(thumbprint, certificateString, new X509Store(storeName, StoreLocation.CurrentUser));
        }

        public static X509Certificate2 FromBase64String(string certificateString)
        {
            return FromBase64String(null, certificateString);
        }

        static X509Certificate2 DoFromBase64String(string thumbprint, string certificateString, X509Store store)
        {
            return DoFromBase64String(thumbprint, certificateString, null, store);
        }

        static X509Certificate2 DoFromBase64String(string thumbprint, string certificateString, string password, X509Store store)
        {
            store.Open(OpenFlags.ReadWrite);
            try
            {
                if (thumbprint != null)
                {
                    Log.Verbose("Loading certificate with thumbprint: " + thumbprint);

                    var certificateFromStore = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false).OfType<X509Certificate2>().FirstOrDefault(CheckThatCertificateWasLoadedWithPrivateKey);
                    if (certificateFromStore != null)
                    {
                        Log.Verbose("Certificate was found in store");
                        return certificateFromStore;
                    }
                }

                Log.Verbose("Loading certificate from disk");
                var raw = Convert.FromBase64String(certificateString);
                var file = Path.Combine(Path.GetTempPath(), "Octo-" + Guid.NewGuid());

                try
                {
                    File.WriteAllBytes(file, raw);

                    var certificate = LoadCertificateWithPrivateKey(file, password);
                    if (CheckThatCertificateWasLoadedWithPrivateKey(certificate) == false)
                    {
                        certificate = LoadCertificateWithPrivateKey(file, password);
                    }
                    
                    if (certificate == null)
                    {
                        throw new CryptographicException("Unable to load X509 Certificate file. The provided certificate or password may be invalid.");
                    }

                    store.Add(certificate);

                    return certificate;
                }
                finally
                {
                    File.Delete(file);
                }
            }
            finally
            {
                store.Close();
            }
        }

        static X509Certificate2 LoadCertificateWithPrivateKey(string file, string password)
        {
            return TryLoadCertificate(file, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet, true)
                ?? TryLoadCertificate(file, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet, true)
                    ?? TryLoadCertificate(file, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet, true)
                        ?? TryLoadCertificate(file, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet, false)
                            ?? TryLoadCertificate(file, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet, false)
                                ?? TryLoadCertificate(file, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet, false);
        }

        static bool CheckThatCertificateWasLoadedWithPrivateKey(X509Certificate2 certificate)
        {
            try
            {
                if (!HasPrivateKey(certificate))
                {
                    var message = new StringBuilder();
                    message.AppendFormat("The X509 certificate {0} was loaded but the private key was not loaded.", certificate.Subject).AppendLine();

                    try
                    {
                        var privateKeyPath = CryptUtils.GetKeyFilePath(certificate);
                        message.AppendLine("The private key file should be located at: " + privateKeyPath);
                        if (!File.Exists(privateKeyPath))
                        {
                            message.AppendLine("However, the current user does not appear to be able to access the private key file, or it does not exist.");
                        }

                        message.AppendLine("Attempting to grant the user " + Environment.UserDomainName + "\\" + Environment.UserName + " access to the certificate private key directory.");

                        try
                        {
                            GrantCurrentUserAccessToPrivateKeyDirectory(privateKeyPath);

                            message.AppendLine("The user should now have read access to the private key. The certificate will be reloaded.");
                        }
                        catch (Exception ex)
                        {
                            message.AppendLine("Unable to grant the current user read access to the private key: " + ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        message.AppendLine("Furthermore, the private key file could not be located: " + ex.Message);
                    }

                    Log.Warn(message.ToString().Trim());

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Warn(ex);
                return false;
            }
        }

        static bool HasPrivateKey(X509Certificate2 certificate2)
        {
            try
            {
                return certificate2.HasPrivateKey && certificate2.PrivateKey != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static X509Certificate2 TryLoadCertificate(string file, string password, X509KeyStorageFlags flags, bool requirePrivateKey)
        {
            try
            {
                var cert = new X509Certificate2(file, password, flags);

                if (!HasPrivateKey(cert) && requirePrivateKey)
                    return null;

                return cert;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void GrantCurrentUserAccessToPrivateKeyDirectory(string privateKeyPath)
        {
            var folderPath = Path.GetDirectoryName(privateKeyPath);
            if (folderPath == null)
                throw new Exception("There was no directory specified in the private key path.");

            var current = WindowsIdentity.GetCurrent();
            if (current == null || current.User == null)
                throw new Exception("There is no current windows identity.");

            var directory = new DirectoryInfo(folderPath);
            var security = directory.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(current.User, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directory.SetAccessControl(security);
        }

        public static X509Certificate2 FromBase64StringPublicKeyOnly(string certificateString)
        {
            var raw = Convert.FromBase64String(certificateString);
            var file = Path.Combine(Path.GetTempPath(), "Octo-" + Guid.NewGuid());
            try
            {
                File.WriteAllBytes(file, raw);
                return new X509Certificate2(file);
            }
            finally
            {
                File.Delete(file);
            }
        }

        public static byte[] Export(X509Certificate2 certificate)
        {
            return certificate.Export(X509ContentType.Pfx);
        }

        public static string ToBase64String(X509Certificate2 certificate)
        {
            var exported = Export(certificate);
            var encoded = Convert.ToBase64String(exported);
            return encoded;
        }

        #region Nested type: CryptUtils

        // This code is from a Microsoft sample that resolves the path to a certificate's private key
        static class CryptUtils
        {
            public static string GetKeyFilePath(X509Certificate2 certificate2)
            {
                var keyFileName = GetKeyFileName(certificate2);
                var keyFileDirectory = GetKeyFileDirectory(keyFileName);

                return Path.Combine(keyFileDirectory, keyFileName);
            }

            static string GetKeyFileName(X509Certificate2 cert)
            {
                var zero = IntPtr.Zero;
                var flag = false;
                var dwFlags = 0u;
                var num = 0;
                string text = null;
                if (CryptAcquireCertificatePrivateKey(cert.Handle, dwFlags, IntPtr.Zero, ref zero, ref num, ref flag))
                {
                    var intPtr = IntPtr.Zero;
                    var num2 = 0;
                    try
                    {
                        if (CryptGetProvParam(zero, CryptGetProvParamType.PP_UNIQUE_CONTAINER, IntPtr.Zero, ref num2, 0u))
                        {
                            intPtr = Marshal.AllocHGlobal(num2);
                            if (CryptGetProvParam(zero, CryptGetProvParamType.PP_UNIQUE_CONTAINER, intPtr, ref num2, 0u))
                            {
                                var array = new byte[num2];
                                Marshal.Copy(intPtr, array, 0, num2);
                                text = Encoding.ASCII.GetString(array, 0, array.Length - 1);
                            }
                        }
                    }
                    finally
                    {
                        if (flag)
                        {
                            CryptReleaseContext(zero, 0u);
                        }
                        if (intPtr != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(intPtr);
                        }
                    }
                }
                if (text == null)
                {
                    throw new InvalidOperationException("Unable to obtain private key file name");
                }
                return text;
            }

            static string GetKeyFileDirectory(string keyFileName)
            {
                var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var text = folderPath + "\\Microsoft\\Crypto\\RSA\\MachineKeys";
                var array = Directory.GetFiles(text, keyFileName);
                string result;
                if (array.Length <= 0)
                {
                    var folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var path = folderPath2 + "\\Microsoft\\Crypto\\RSA\\";
                    array = Directory.GetDirectories(path);
                    if (array.Length > 0)
                    {
                        var array2 = array;
                        for (var i = 0; i < array2.Length; i++)
                        {
                            var text2 = array2[i];
                            array = Directory.GetFiles(text2, keyFileName);
                            if (array.Length != 0)
                            {
                                result = text2;
                                return result;
                            }
                        }
                    }
                    throw new InvalidOperationException("Unable to locate private key file directory");
                }
                result = text;
                return result;
            }

            [DllImport("crypt32", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern bool CryptAcquireCertificatePrivateKey(IntPtr pCert, uint dwFlags, IntPtr pvReserved, ref IntPtr phCryptProv, ref int pdwKeySpec, ref bool pfCallerFreeProv);

            [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern bool CryptGetProvParam(IntPtr hCryptProv, CryptGetProvParamType dwParam, IntPtr pvData, ref int pcbData, uint dwFlags);

            [DllImport("advapi32", SetLastError = true)]
            internal static extern bool CryptReleaseContext(IntPtr hProv, uint dwFlags);

            #region Nested type: CryptGetProvParamType

            internal enum CryptGetProvParamType
            {
                PP_ENUMALGS = 1,
                PP_ENUMCONTAINERS,
                PP_IMPTYPE,
                PP_NAME,
                PP_VERSION,
                PP_CONTAINER,
                PP_CHANGE_PASSWORD,
                PP_KEYSET_SEC_DESCR,
                PP_CERTCHAIN,
                PP_KEY_TYPE_SUBTYPE,
                PP_PROVTYPE = 16,
                PP_KEYSTORAGE,
                PP_APPLI_CERT,
                PP_SYM_KEYSIZE,
                PP_SESSION_KEYSIZE,
                PP_UI_PROMPT,
                PP_ENUMALGS_EX,
                PP_ENUMMANDROOTS = 25,
                PP_ENUMELECTROOTS,
                PP_KEYSET_TYPE,
                PP_ADMIN_PIN = 31,
                PP_KEYEXCHANGE_PIN,
                PP_SIGNATURE_PIN,
                PP_SIG_KEYSIZE_INC,
                PP_KEYX_KEYSIZE_INC,
                PP_UNIQUE_CONTAINER,
                PP_SGC_INFO,
                PP_USE_HARDWARE_RNG,
                PP_KEYSPEC,
                PP_ENUMEX_SIGNING_PROT,
                PP_CRYPT_COUNT_KEY_USE
            }

            #endregion
        }

        #endregion
    }
}