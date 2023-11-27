param ([string] $version)

$path = "C:\Program Files\Octopus Deploy\Tentacle$version\"

If(!(test-path -PathType container $path))
{
    choco install octopusdeploy.tentacle --version=$version --force -y
    xcopy /E /I "C:\Program Files\Octopus Deploy\Tentacle\" $path
}