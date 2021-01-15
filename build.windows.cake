// This file uses Windows APIs that are incompatible with other operating systems.
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

var testDir = "./_test";

Task("Test-WindowsInstallerPermissions")
    .Does(() =>
    {
        InTestSuite("Test-LinuxPackages", () => {
            CleanDirectories(testDir);
            CreateDirectory(testDir);

            var installers = GetFiles($"{artifactsDir}/msi/*x64.msi");

            if (!installers.Any())
                throw new Exception($"Expected to find at least one installer in the directory {artifactsDir}");

            foreach (var installer in installers)
            {
                TestInstallerPermissions(installer);
            }
        });
    });

void TestInstallerPermissions(FilePath msiPath)
{
    var testName = msiPath.GetFilenameWithoutExtension().FullPath;
    InTest($"{testName}", () => {
        var destination = Path.GetFullPath($"{testDir}/install/{testName}");

        InstallMsi(msiPath, destination);

        try {
            var builtInUsersHaveWriteAccess = DoesSidHaveRightsToDirectory(destination, WellKnownSidType.BuiltinUsersSid, FileSystemRights.AppendData, FileSystemRights.CreateFiles);
            if (builtInUsersHaveWriteAccess)
            {
                throw new Exception($"The installation destination {destination} has write permissions for the user BUILTIN\\Users. Expected write permissions to be removed by the installer.");
            }
        }
        finally {
            UninstallMsi(msiPath);
        }

        Information($"BUILTIN\\Users do not have write access to {destination}. Hooray!");
    });
}

void InstallMsi(FilePath msiPath, string destination)
{
    var installerName = msiPath.GetFilenameWithoutExtension();
    var installLogName = Path.Combine(testDir, $"{installerName.FullPath}.install.log");
    var installerPath = Path.GetFullPath(msiPath.FullPath); // The format of FilePath doesn't work with msiexec

    Information($"Installing {installerPath} to {destination}");

    var arguments = $"/i {installerPath} /QN INSTALLLOCATION={destination} /L*V {installLogName}";
    Information($"Running msiexec {arguments}");
    var installationProcess = Process.Start("msiexec", arguments);
    installationProcess.WaitForExit();
    CopyFileToDirectory(installLogName, artifactsDir);
    if (installationProcess.ExitCode != 0) {
        throw new Exception($"The installation process exited with a non-zero exit code ({installationProcess.ExitCode}). Check the log {installLogName} for details.");
    }
}

void UninstallMsi(FilePath msiPath)
{
    Information($"Uninstalling {msiPath}");
    var uninstallLogName = Path.Combine(testDir, $"{msiPath.GetFilenameWithoutExtension().FullPath}.uninstall.log");
    var installerPath = Path.GetFullPath(msiPath.FullPath); // The format of FilePath doesn't work with msiexec

    var arguments = $"/x {installerPath} /QN /L*V {uninstallLogName}";
    Information($"Running msiexec {arguments}");
    var uninstallProcess = Process.Start("msiexec", arguments);
    uninstallProcess.WaitForExit();
    CopyFileToDirectory(uninstallLogName, artifactsDir);
}

bool DoesSidHaveRightsToDirectory(string directory, WellKnownSidType sid, params FileSystemRights[] rights)
{
    var destinationInfo = new DirectoryInfo(directory);
    var acl = destinationInfo.GetAccessControl();
    var identifier = new SecurityIdentifier(sid, null);
    return acl.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier))
        .Cast<FileSystemAccessRule>()
        .Where(r => r.IdentityReference.Value == identifier.Value)
        .Where(r => r.AccessControlType == AccessControlType.Allow)
        .Where(r => rights.Any(right => r.FileSystemRights.HasFlag(right)))
        .Any();
}

#load "build.cake"