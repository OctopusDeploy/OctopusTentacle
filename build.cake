//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0007"
#tool "nuget:?package=WiX&version=3.10.3"

#addin "Cake.FileHelpers"

using Path = System.IO.Path;
using Dir = System.IO.Directory;
using System.Xml;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var verbosity = Argument<Verbosity>("verbosity", Verbosity.Quiet);

var signingCertificatePath = Argument("signing_certicifate_path", "./certificates/OctopusDevelopment.pfx");
var signingCertificatPassword = Argument("signing_certificate_password", "Password01!");

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

GitVersion gitVersion;

var cleanups = new List<Action>();

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////
Setup(context =>
{
    context.Tools.RegisterFile("./signtool.exe");
    CreateDirectory(installerPackageDir);
    CreateDirectory(binariesPackageDir);
    CreateDirectory(installerDir);
    CreateDirectory(artifactsDir);
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
    .IsDependentOn("__SignBuiltFiles")
    .IsDependentOn("__CreateTentacleInstaller")
    .IsDependentOn("__CreateChocolateyPackage")
    .IsDependentOn("__CreateInstallerNuGet")
    .IsDependentOn("__CreateBinariesNuGet")
    .IsDependentOn("__CopyToLocalPackages");

Task("__Version")
    .IsDependentOn("__GitVersionAssemblies")
    .Does(() =>
{
    if(BuildSystem.IsRunningOnTeamCity)
        BuildSystem.TeamCity.SetBuildNumber(gitVersion.NuGetVersion);

    Information("Building OctopusClients v{0}", gitVersion.NuGetVersion);
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
    CleanDirectory(installerPackageDir);
    CleanDirectory(binariesPackageDir);
    CleanDirectory(installerDir);
    CleanDirectory(artifactsDir);
});

Task("__Restore")
    .Does(() => NuGetRestore("./source/Tentacle.sln"));

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

Task("__SignBuiltFiles")
    .Does(() =>
{
    var filesToSign = new string[] {
        "./source/Octopus.Tentacle/bin/net45/Octopus*.dll",
        "./source/Octopus.Tentacle/bin/net45/Octopus*.exe",
        "./source/Octopus.Tentacle/bin/net45/Tentacle.exe",
        "./source/Octopus.Manager.Tentacle/bin/Octopus*.dll",
        "./source/Octopus.Manager.Tentacle/bin/Octopus*.exe"
    };

    SignAndTimeStamp(filesToSign);
});


Task("__CreateTentacleInstaller")
    .IsDependentOn("__UpdateWixVersion")
    .Does(() =>
{
    CopyFiles("./source/Octopus.Manager.Tentacle/bin/*", installerDir);
    CopyFiles("./source/Octopus.Tentacle/bin/net45/*", installerDir);

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
        Information($"Checksum: Octopus.Tentacle.msi = {checksum}");

        var checksum64 = CalculateFileHash(File($"{artifactsDir}/Octopus.Tentacle.{gitVersion.NuGetVersion}-x64.msi"));
        Information($"Checksum: Octopus.Tentacle-x64.msi = {checksum64}");
        
        Information("Default " + System.Text.Encoding.Default.GetString(checksum.ComputedHash));
        Information("ASCII " + System.Text.Encoding.ASCII.GetString(checksum.ComputedHash));
        Information("UTF8 " + System.Text.Encoding.UTF8.GetString(checksum.ComputedHash));
        Information("UTF7 " + System.Text.Encoding.UTF7.GetString(checksum.ComputedHash));
        Information("UTF32 " + System.Text.Encoding.UTF32.GetString(checksum.ComputedHash));
        Information("Unicode " + System.Text.Encoding.Unicode.GetString(checksum.ComputedHash));
        Information("BitConverter " + BitConverter.ToString(checksum.ComputedHash));
                
        var hashValue = "";
        for (var i = 0; i < checksum.ComputedHash.Length; ++i) {
        	hashValue += Convert.ToChar( checksum.ComputedHash[i]);
        }
        Information("hashValue " + hashValue);
       

        var chocolateyInstallScriptPath = "./source/Chocolatey/chocolateyInstall.ps1";
        RestoreFileOnCleanup(chocolateyInstallScriptPath);

        ReplaceTextInFiles(chocolateyInstallScriptPath, "0.0.0", gitVersion.NuGetVersion);
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum>", System.Text.Encoding.Default.GetString(checksum.ComputedHash));
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksumtype>", checksum.Algorithm.ToString());
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum64>", System.Text.Encoding.Default.GetString(checksum64.ComputedHash));
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
    .Does(() =>
{
    CreateDirectory($"{binariesPackageDir}/lib");
    CopyFileToDirectory($"./source/Octopus.Manager.Tentacle/bin/Octopus.Manager.Tentacle.exe", $"{binariesPackageDir}/lib");
    CleanBinariesDirectory($"{binariesPackageDir}/lib");

    CreateDirectory($"{binariesPackageDir}/build/Tentacle");
    CopyFiles($"./source/Octopus.Tentacle/bin/net45/*", $"{binariesPackageDir}/build/Tentacle");
    CleanBinariesDirectory($"{binariesPackageDir}/build/Tentacle");

    CopyFileToDirectory("./source/Octopus.Tentacle/Tentacle.Binaries.nuspec", binariesPackageDir);
    CopyFileToDirectory("./source/Octopus.Tentacle/Tentacle.Binaries.targets", $"{binariesPackageDir}/build");

    NuGetPack(Path.Combine(binariesPackageDir, "Tentacle.Binaries.nuspec"), new NuGetPackSettings {
        Version = gitVersion.NuGetVersion,
        OutputDirectory = artifactsDir
    });
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

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////
RunTarget(target);