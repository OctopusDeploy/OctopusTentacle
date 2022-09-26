// ReSharper disable RedundantUsingDirective

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.SignTool;
using Serilog;

public static class Signing
{
    // Keep this list in order by most likely to succeed
    static readonly string[] SigningTimestampUrls =
    {
        "http://timestamp.digicert.com?alg=sha256",
        "http://timestamp.comodoca.com"
    };

    public static void Sign(params AbsolutePath[] files) =>
        Logging.InBlock("Signing and timestamping...", () =>
        {
            foreach (var file in files)
            {
                if (!FileSystemTasks.FileExists(file)) throw new Exception($"File {file} does not exist");
                var fileInfo = new FileInfo(file);

                if (fileInfo.IsReadOnly)
                {
                    Log.Information($"{file} is readonly. Making it writeable.");
                    fileInfo.IsReadOnly = false;
                }
            }

            if (string.IsNullOrEmpty(Build.AzureKeyVaultUrl)
                && string.IsNullOrEmpty(Build.AzureKeyVaultAppId)
                && string.IsNullOrEmpty(Build.AzureKeyVaultTenantId)
                && string.IsNullOrEmpty(Build.AzureKeyVaultAppSecret)
                && string.IsNullOrEmpty(Build.AzureKeyVaultCertificateName))
            {
                Log.Information("Signing files using signtool and the self-signed development code signing certificate.");
                SignWithSignTool(files);
            }
            else
            {
                Log.Information("Signing files using azuresigntool and the production code signing certificate.");
                SignWithAzureSignTool(files);
            }
        });

    public static bool HasAuthenticodeSignature(AbsolutePath fileInfo)
    {
        // note: Doesn't check if existing signatures are valid, only that one exists
        // source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
        try
        {
            X509Certificate.CreateFromSignedFile(fileInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void SignWithSignTool(AbsolutePath[] files)
    {
        var lastException = default(Exception);
        foreach (var timestampUrl in SigningTimestampUrls)
        {
            Logging.InBlock($"Trying to time stamp using {timestampUrl}", () =>
            {
                try
                {
                    SignToolTasks.SignTool(settings => settings
                        .SetFile(Build.SigningCertificatePath)
                        .SetPassword(Build.SigningCertificatePassword)
                        .SetFileDigestAlgorithm("sha256")
                        .SetRfc3161TimestampServerUrl(timestampUrl)
                        .SetProcessToolPath(NukeBuild.RootDirectory / "signtool.exe")
                        .SetDescription("Octopus Tentacle Agent")
                        .SetUrl("https://octopus.com")
                        .SetFiles(files.Select(x => x.ToString())));
                }
                catch (Exception e)
                {
                    lastException = e;
                }
            });

            if (lastException == null) return;
        }

        if (lastException != null) throw lastException;
    }

    static void SignWithAzureSignTool(AbsolutePath[] files)
    {
        var lastException = default(Exception);
        foreach (var timestampUrl in SigningTimestampUrls)
        {
            Logging.InBlock($"Trying to time stamp using {timestampUrl}", () =>
            {
                try
                {
                    var arguments = "sign " +
                        $"--azure-key-vault-url \"{Build.AzureKeyVaultUrl}\" " +
                        $"--azure-key-vault-client-id \"{Build.AzureKeyVaultAppId}\" " +
                        $"--azure-key-vault-tenant-id \"{Build.AzureKeyVaultTenantId}\" " +
                        $"--azure-key-vault-client-secret \"{Build.AzureKeyVaultAppSecret}\" " +
                        $"--azure-key-vault-certificate \"{Build.AzureKeyVaultCertificateName}\" " +
                        "--file-digest sha256 " +
                        $"--timestamp-rfc3161 \"{timestampUrl}\" ";

                    arguments = files.Aggregate(arguments, (current, file) => current + $"\"{file}\" ");

                    Build.AzureSignTool(arguments);

                    Log.Information($"Finished signing {files.Length} files.");
                }
                catch (Exception e)
                {
                    lastException = e;
                }
            });

            if (lastException == null) return;
        }

        if (lastException != null) throw lastException;
    }
}