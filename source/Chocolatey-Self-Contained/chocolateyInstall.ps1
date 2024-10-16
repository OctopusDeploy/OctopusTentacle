Install-ChocolateyPackage `
    -PackageName 'OctopusDeploy.Tentacle.SelfContained' `
    -FileType 'msi' `
    -SilentArgs '/quiet' `
    -Url 'https://download.octopusdeploy.com/octopus/Octopus.Tentacle.0.0.0-net8.0-windows-win-x86.msi' `
    -Url64bit 'https://download.octopusdeploy.com/octopus/Octopus.Tentacle.0.0.0-net8.0-windows-win-x64.msi' `
    -Checksum '<checksum>' `
    -ChecksumType '<checksumtype>' `
    -Checksum64 '<checksum64>' `
    -ChecksumType64 '<checksumtype64>'
