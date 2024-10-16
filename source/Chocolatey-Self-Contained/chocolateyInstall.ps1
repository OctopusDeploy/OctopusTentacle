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
