//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0"
#tool "nuget:?package=WiX&version=3.11.1"
#addin "Cake.FileHelpers&version=3.2.0"
#addin "Cake.Docker&version=0.10.0"

using Path = System.IO.Path;
using Dir = System.IO.Directory;
using System.Xml;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var verbosity = Argument<Verbosity>("verbosity", Verbosity.Quiet);

var signingCertificatePath = Argument("signing_certificate_path", "./certificates/OctopusDevelopment.pfx");
var signingCertificatPassword = Argument("signing_certificate_password", "Password01!");

var gpgSigningCertificatePath = Argument("gpg_signing_certificate_path", "./certificates/octopus-privkey.asc");
var gpgSigningCertificatePassword = Argument("gpg_signing_certificate_password", "Password01!");

var awsAccessKeyId = Argument("aws_access_key_id", "XXXX");
var awsSecretAccessKey = Argument("aws_secret_access_key", "YYYY");

// Keep this list in order by most likely to succeed
var signingTimestampUrls = new string[] {
    "http://timestamp.globalsign.com/scripts/timestamp.dll",
    "http://www.startssl.com/timestamp",
    "http://timestamp.comodoca.com/rfc3161",
    "http://timestamp.verisign.com/scripts/timstamp.dll",
    "http://tsa.starfieldtech.com"};

var installerPackageDir = "./build/package/installer";
var binariesPackageDir = "./build/package/binaries";
var installerDir = "./build/installer";
var artifactsDir = "./build/artifacts";
var localPackagesDir = "../LocalPackages";
var corePublishDir = "./build/publish";

GitVersion gitVersion;

var cleanups = new List<Action>();

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    context.Tools.RegisterFile("./signtool.exe");
});

Teardown(context =>
{
    Information("Cleaning up");
    foreach(var cleanup in cleanups)
        cleanup();

    Information("Finished running tasks for build v{0}", gitVersion.NuGetVersion);
});


//////////////////////////////////////////////////////////////////////
//  PRIVATE TASKS
//////////////////////////////////////////////////////////////////////
Task("__Default")
    .IsDependentOn("__Version")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__Build")
    .IsDependentOn("__Test")
    .IsDependentOn("__DotnetPublish")
    .IsDependentOn("__SignBuiltFiles")
    .IsDependentOn("__CreateTentacleInstaller")
    .IsDependentOn("__CreateChocolateyPackage")
    .IsDependentOn("__CreateInstallerNuGet")
    .IsDependentOn("__CreateBinariesNuGet")
    .IsDependentOn("__CopyToLocalPackages");

Task("__OsPackage")
    .IsDependentOn("__Version")
    .IsDependentOn("__Clean")
    .IsDependentOn("__Restore")
    .IsDependentOn("__Build")
    .IsDependentOn("__PublishLinux")
    .IsDependentOn("__BuildToolsContainer")
    .IsDependentOn("__CreateDebianPackage")
    .IsDependentOn("__CreateAptRepoInS3");

Task("__PublishLinux")
    .Does(() =>
{
    DotNetCorePublish(
        "./source/Octopus.Tentacle/Octopus.Tentacle.csproj",
        new DotNetCorePublishSettings
        {
            Framework = "netcoreapp2.2",
            Configuration = configuration,
            SelfContained = true,
            Runtime = "linux-x64"
        }
    );
});

Task("__BuildToolsContainer")
    .Does(() =>
{
    DockerBuild(new DockerImageBuildSettings { Tag = new string[] { "tentacle-tools" } }, @"tools\debian-tools");
});

Task("__CreateDebianPackage")
    .Does(() =>
{
    DockerRunWithoutResult(new DockerContainerRunSettings {
        Rm = true,
        Tty = true,
        Env = new string[] { $"VERSION={gitVersion.SemVer}" },
        Volume = new string[] { $"{Path.Combine(Environment.CurrentDirectory, "source/Octopus.Tentacle/bin/netcoreapp2.2/linux-x64")}:/app" }
    }, "tentacle-tools", "/build/package.sh");

    CopyFiles("./source/Octopus.Tentacle/bin/netcoreapp2.2/linux-x64/*.deb", artifactsDir);
});

Task("__CreateAptRepoInS3")
    .Does(() =>
{
    CreateDirectory("./certificates/temp");

    CopyFileToDirectory(gpgSigningCertificatePath, "./certificates/temp");

    DockerRunWithoutResult(new DockerContainerRunSettings {
        Rm = true,
        Tty = true,
        Env = new string[] {
            $"VERSION={gitVersion.SemVer}",
            $"AWS_ACCESS_KEY_ID={awsAccessKeyId}",
            $"AWS_SECRET_ACCESS_KEY={awsSecretAccessKey}",
            $"GPG_PASSPHRASE={gpgSigningCertificatePassword}",
            $"GPG_PRIVATEKEYFILE={Path.GetFileName(gpgSigningCertificatePath)}"
        },
        Volume = new string[] {
            $"{Path.Combine(Environment.CurrentDirectory, "source/Octopus.Tentacle/bin/netcoreapp2.2/linux-x64")}:/app",
            $"{Path.Combine(Environment.CurrentDirectory, "certificates", "temp")}:/certs",
        }
    }, "tentacle-tools", "/build/publish.sh");

    DeleteDirectory("./certificates/temp", true);
});

Task("__Version")
    .IsDependentOn("__GitVersionAssemblies")
    .Does(() =>
{
    if(BuildSystem.IsRunningOnTeamCity)
        BuildSystem.TeamCity.SetBuildNumber(gitVersion.NuGetVersion);

    Information("Building OctopusTentacle v{0}", gitVersion.NuGetVersion);
});

Task("__GitVersionAssemblies")
    .Does(() =>
{
    var gitVersionFile = "./source/Solution Items/VersionInfo.cs";

    RestoreFileOnCleanup(gitVersionFile);

    gitVersion = GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true,
        UpdateAssemblyInfoFilePath = gitVersionFile
    });

    ReplaceRegexInFiles(gitVersionFile, "BranchName = \".*?\"", $"BranchName = \"{gitVersion.BranchName}\"");
    ReplaceRegexInFiles(gitVersionFile, "NuGetVersion = \".*?\"", $"NuGetVersion = \"{gitVersion.NuGetVersion}\"");
});

Task("__Clean")
    .Does(() =>
{
    CleanDirectories("./source/**/bin");
    CleanDirectories("./source/**/obj");
    CleanDirectory("./build");
    CreateDirectory(installerPackageDir);
    CreateDirectory(binariesPackageDir);
    CreateDirectory(installerDir);
    CreateDirectory(artifactsDir);
});

Task("__Restore")
    .Does(() => {
        DotNetCoreRestore("./source/Tentacle.sln");
        NuGetRestore(
            "./source/Octopus.Manager.Tentacle/Octopus.Manager.Tentacle.csproj",
            new NuGetRestoreSettings { PackagesDirectory = "./source/packages" }
        );
    });

Task("__Build")
    .Does(() =>
{
    MSBuild("./source/Tentacle.sln", settings =>
        settings
            .SetConfiguration(configuration)
            .SetVerbosity(verbosity)
            .UseToolVersion(MSBuildToolVersion.VS2017)
    );
});

Task("__Test")
    .IsDependentOn("__Build")
    .Does(() =>
{
    DotNetCoreTest("./source/Octopus.Tentacle.Tests/Octopus.Tentacle.Tests.csproj", new DotNetCoreTestSettings
    {
        Configuration = configuration,
        NoBuild = true
    });
});

Task("__DotnetPublish")
    .IsDependentOn("__Build")
    .Does(() =>  {

        foreach(var rid in GetProjectRuntimeIds(@"./source/Octopus.Tentacle/Octopus.Tentacle.csproj"))
        {
            DotNetCorePublish(
                "./source/Octopus.Tentacle/Octopus.Tentacle.csproj",
                new DotNetCorePublishSettings
                {
                    Framework = "netcoreapp2.2",
                    Configuration = configuration,
                    OutputDirectory = $"{corePublishDir}/{rid}",
                    Runtime = rid,
                    ArgumentCustomization = args => args.Append($"/p:Version={gitVersion.NuGetVersion}")
                }
            );
        }
    });

private IEnumerable<string> GetProjectRuntimeIds(string projectFile)
{
    var doc = new XmlDocument();
    doc.Load(projectFile);
    var rids = doc.SelectSingleNode("Project/PropertyGroup/RuntimeIdentifiers").InnerText;
    return rids.Split(';');
}

Task("__SignBuiltFiles")
    .Does(() =>
{
    // check that any unsigned libraries get signed, to play nice with security scanning tools
    // refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
    // note: we are signing both dll's we have written & some we haven't, not because we are
    //       claiming we own them, but rather asserting that they are distributed by us, and
    //       have not been subsequently altered
    var filesToSign =
        GetFiles($"{corePublishDir}/**/*.exe")
        //.Union(GetFiles($"{corePublishDir}/**/*.exe"))
        .Union(GetFiles($"./source/Octopus.Tentacle/bin/net452/**/*.dll"))
        .Union(GetFiles($"./source/Octopus.Tentacle/bin/net452/**/*.exe"))
        .Union(GetFiles($"./source/Octopus.Manager.Tentacle/bin/*.dll"))
        .Union(GetFiles($"./source/Octopus.Manager.Tentacle/bin/*.exe"))
            .Where(f => !HasAuthenticodeSignature(f))
            .Select(f => f.FullPath)
            .ToArray();

    SignAndTimeStamp(filesToSign);
});

// note: Doesn't check if existing signatures are valid, only that one exists
// source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
private bool HasAuthenticodeSignature(FilePath fileInfo)
{
    try
    {
        X509Certificate.CreateFromSignedFile(fileInfo.FullPath);
        return true;
    }
    catch
    {
        return false;
    }
}

Task("__CreateTentacleInstaller")
    .IsDependentOn("__UpdateWixVersion")
    .Does(() =>
{
    CopyFiles("./source/Octopus.Manager.Tentacle/bin/*", installerDir);
    CopyFiles("./source/Octopus.Tentacle/bin/net452/*", installerDir);

    CleanBinariesDirectory(installerDir);

    InBlock("Running HEAT to generate the installer contents...", () => GenerateInstallerContents());

    InBlock("Building 32-bit installer", () => BuildInstallerForPlatform(PlatformTarget.x86));
    InBlock("Building 64-bit installer", () => BuildInstallerForPlatform(PlatformTarget.x64));

    CopyFiles($"{artifactsDir}/*.msi", installerPackageDir);
});

Task("__UpdateWixVersion")
    .Does(() =>
{
    var installerProductFile = "./source/Octopus.Tentacle.Installer/Product.wxs";
    RestoreFileOnCleanup(installerProductFile);

    var xmlDoc = new XmlDocument();
    xmlDoc.Load(installerProductFile);

    var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
    nsmgr.AddNamespace("wi", "http://schemas.microsoft.com/wix/2006/wi");

    var product = xmlDoc.SelectSingleNode("//wi:Product", nsmgr);
    product.Attributes["Version"].Value = gitVersion.MajorMinorPatch;
    xmlDoc.Save(installerProductFile);
});

Task("__CreateChocolateyPackage")
    .Does(() =>
{
    InBlock ("Create Chocolatey package...", () =>
    {
        var checksum = CalculateFileHash(File($"{artifactsDir}/Octopus.Tentacle.{gitVersion.NuGetVersion}.msi"));
        var checksumValue = BitConverter.ToString(checksum.ComputedHash).Replace("-", "");
        Information($"Checksum: Octopus.Tentacle.msi = {checksumValue}");

        var checksum64 = CalculateFileHash(File($"{artifactsDir}/Octopus.Tentacle.{gitVersion.NuGetVersion}-x64.msi"));
        var checksum64Value = BitConverter.ToString(checksum64.ComputedHash).Replace("-", "");
        Information($"Checksum: Octopus.Tentacle-x64.msi = {checksum64Value}");

        var chocolateyInstallScriptPath = "./source/Chocolatey/chocolateyInstall.ps1";
        RestoreFileOnCleanup(chocolateyInstallScriptPath);

        ReplaceTextInFiles(chocolateyInstallScriptPath, "0.0.0", gitVersion.NuGetVersion);
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum>", checksumValue);
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksumtype>", checksum.Algorithm.ToString());
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum64>", checksum64Value);
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksumtype64>", checksum64.Algorithm.ToString());

        var chocolateyArtifactsDir = $"{artifactsDir}/Chocolatey";
        CreateDirectory(chocolateyArtifactsDir);

        NuGetPack("./source/Chocolatey/OctopusDeploy.Tentacle.nuspec", new NuGetPackSettings {
            Version = gitVersion.NuGetVersion,
            OutputDirectory = chocolateyArtifactsDir,
            NoPackageAnalysis = true
        });
    });
});

Task("__CreateInstallerNuGet")
    .Does(() =>
{
    CopyFiles($"{artifactsDir}/*.msi", installerPackageDir);
    CopyFileToDirectory("./source/Octopus.Tentacle/Tentacle.Installers.nuspec", installerPackageDir);

    NuGetPack(Path.Combine(installerPackageDir, "Tentacle.Installers.nuspec"), new NuGetPackSettings {
        Version = gitVersion.NuGetVersion,
        OutputDirectory = artifactsDir
    });
});

Task("__CreateBinariesNuGet")
    .IsDependentOn("__SignBuiltFiles")
    .Does(() =>
{
    CreateDirectory($"{binariesPackageDir}/lib/net452");
    CopyFileToDirectory($"./source/Octopus.Manager.Tentacle/bin/Octopus.Manager.Tentacle.exe", $"{binariesPackageDir}/lib/net452");
    CleanBinariesDirectory($"{binariesPackageDir}/lib/net452");

    CreateDirectory($"{binariesPackageDir}/build/net452/Tentacle");
    CopyFiles($"./source/Octopus.Tentacle/bin/net452/*", $"{binariesPackageDir}/build/net452/Tentacle");
    CleanBinariesDirectory($"{binariesPackageDir}/build/net452/Tentacle");
    CopyFileToDirectory("./source/Octopus.Tentacle/Tentacle.Binaries.nuspec", binariesPackageDir);
    CopyFileToDirectory("./source/Octopus.Tentacle/Tentacle.Binaries.targets", $"{binariesPackageDir}/build/net452");
    NuGetPack(Path.Combine(binariesPackageDir, "Tentacle.Binaries.nuspec"), new NuGetPackSettings {
        Version = gitVersion.NuGetVersion,
        OutputDirectory = artifactsDir
    });

    foreach(var rid in GetProjectRuntimeIds(@"./source/Octopus.Tentacle/Octopus.Tentacle.csproj"))
    {
        CleanDirectory(binariesPackageDir);
        CreateDirectory($"{binariesPackageDir}/build/netcoreapp2.2/Tentacle");
        CopyFiles($"{corePublishDir}/{rid}/*", $"{binariesPackageDir}/build/netcoreapp2.2/Tentacle");
        DeleteFile($"{binariesPackageDir}/build/netcoreapp2.2/Tentacle/Tentacle.exe.manifest");
        CleanBinariesDirectory($"{binariesPackageDir}/build/netcoreapp2.2/Tentacle");
        CopyFileToDirectory("./source/Octopus.Tentacle/Tentacle.Binaries.nuspec", binariesPackageDir);
        CopyFile("./source/Octopus.Tentacle/Tentacle.Binaries.targets", $"{binariesPackageDir}/build/netcoreapp2.2/Tentacle.Binaries.{rid}.targets");

        NuGetPack(Path.Combine(binariesPackageDir, $"Tentacle.Binaries.nuspec"), new NuGetPackSettings {
            Version = gitVersion.NuGetVersion,
            OutputDirectory = artifactsDir,
            Id = $"Tentacle.Binaries.{rid}"
        });
    }
});

Task("__CopyToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .IsDependentOn("__CreateInstallerNuGet")
    .Does(() =>
{
    CreateDirectory(localPackagesDir);
    CopyFileToDirectory(Path.Combine(artifactsDir, $"Tentacle.Binaries.{gitVersion.NuGetVersion}.nupkg"), localPackagesDir);
});

private void InBlock(string block, Action action)
{
    if (TeamCity.IsRunningOnTeamCity)
        TeamCity.WriteStartBlock(block);
    else
        Information($"Starting {block}");

    try
    {
        action();
    }
    finally
    {
        if (TeamCity.IsRunningOnTeamCity)
            TeamCity.WriteEndBlock(block);
        else
            Information($"Finished {block}");
    }
}

private void RestoreFileOnCleanup(string file)
{
    var contents = System.IO.File.ReadAllBytes(file);
    cleanups.Add(() => {
        Information("Restoring {0}", file);
        System.IO.File.WriteAllBytes(file, contents);
    });
}

private void GenerateInstallerContents()
{
    var harvestDirectory = Directory(installerDir);

    var harvestFile = "./source/Octopus.Tentacle.Installer/Tentacle.Generated.wxs";
    RestoreFileOnCleanup(harvestFile);

    var heatSettings = new HeatSettings {
        NoLogo = true,
        GenerateGuid = true,
        SuppressFragments = true,
        SuppressRootDirectory = true,
        SuppressRegistry = true,
        SuppressUniqueIds = true,
        ComponentGroupName = "TentacleComponents",
        PreprocessorVariable = "var.TentacleSource",
        DirectoryReferenceId = "INSTALLLOCATION"
    };

    WiXHeat(harvestDirectory, File(harvestFile), WiXHarvestType.Dir, heatSettings);
}

private void BuildInstallerForPlatform(PlatformTarget platformTarget)
{
    var allowUpgrade = !string.Equals(gitVersion.PreReleaseLabel, "alpha");

    MSBuild("./source/Octopus.Tentacle.Installer/Octopus.Tentacle.Installer.wixproj", settings =>
        settings
            .SetConfiguration(configuration)
            .WithProperty("AllowUpgrade", allowUpgrade.ToString())
            .SetVerbosity(verbosity)
            .SetPlatformTarget(platformTarget)
            .UseToolVersion(MSBuildToolVersion.VS2015)
            .WithTarget("build")
    );
    var builtMsi = File($"./source/Octopus.Tentacle.Installer/bin/{platformTarget}/Octopus.Tentacle.msi");

    SignAndTimeStamp(builtMsi);

    var platformStr = platformTarget == PlatformTarget.x64
        ? "-x64"
        : "";

    var artifactDestination = $"{artifactsDir}/Octopus.Tentacle.{gitVersion.NuGetVersion}{platformStr}.msi";

    MoveFile(builtMsi, File(artifactDestination));
}

private void CleanBinariesDirectory(string directory)
{
    Information($"Cleaning {directory}");
    DeleteFiles($"{directory}/*.xml");
}

private void SignAndTimeStamp(params string[] paths)
{
    var allFiles = new List<FilePath>();
    foreach (var path in paths)
    {
        var files = GetFiles(path);
        allFiles.AddRange(files);
    }
    SignAndTimeStamp(allFiles.ToArray());
}

private void SignAndTimeStamp(params FilePath[] assemblies)
{
    var lastException = default(Exception);
    var signSettings = new SignToolSignSettings
    {
        CertPath = File(signingCertificatePath),
        Password = signingCertificatPassword,
        DigestAlgorithm = SignToolDigestAlgorithm.Sha256,
        Description = "Octopus Tentacle Agent",
        DescriptionUri = new Uri("http://octopus.com")
    };

    foreach (var url in signingTimestampUrls)
    {
        Information($"  Trying to time stamp {assemblies} using {url}");
        signSettings.TimeStampUri = new Uri(url);
        try
        {
            Sign(assemblies, signSettings);
            return;
        }
        catch (Exception ex)
        {
            lastException = ex;
        }
    }
    throw(lastException);
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////
Task("Default")
    .IsDependentOn("__Default");

Task("OsPackage")
    .IsDependentOn("__OsPackage");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);