Install-ChocolateyPackage `
    -PackageName 'OctopusDeploy.Tentacle' `
    -FileType 'msi' `
    -SilentArgs '/quiet' `
    -Url 'https://download.octopusdeploy.com/octopus/Octopus.Tentacle.0.0.0.msi' `
    -Url64bit 'https://download.octopusdeploy.com/octopus/Octopus.Tentacle.0.0.0-x64.msi' `
    -Checksum '<checksum-net-framework>' `
    -ChecksumType '<checksumtype-net-framework>' `
    -Checksum64 '<checksum64-net-framework>' `
    -ChecksumType64 '<checksumtype64-net-framework>'
Install-ChocolateyPackage `
    -PackageName 'OctopusDeploy.Tentacle.SelfContained' `
    -FileType 'msi' `
    -SilentArgs '/quiet' `
    -Url 'https://download.octopusdeploy.com/octopus/Octopus.Tentacle.0.0.0-net8.0-windows-win-x86.msi' `
    -Url64bit 'https://download.octopusdeploy.com/octopus/Octopus.Tentacle.0.0.0-net8.0-windows-win-x64.msi' `
    -Checksum '<checksum-self-contained>' `
    -ChecksumType '<checksumtype-self-contained>' `
    -Checksum64 '<checksum64-self-contained>' `
    -ChecksumType64 '<checksumtype64-self-contained>'

