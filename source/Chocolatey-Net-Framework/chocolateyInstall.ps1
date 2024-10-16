Install-ChocolateyPackage `
    -PackageName 'OctopusDeploy.Tentacle' `
    -FileType 'msi' `
    -SilentArgs '/quiet' `
    -Url 'https://download.octopusdeploy.com/octopus/Octopus.Tentacle.0.0.0.msi' `
    -Url64bit 'https://download.octopusdeploy.com/octopus/Octopus.Tentacle.0.0.0-x64.msi' `
    -Checksum '<checksum>' `
    -ChecksumType '<checksumtype>' `
    -Checksum64 '<checksum64>' `
    -ChecksumType64 '<checksumtype64>'
