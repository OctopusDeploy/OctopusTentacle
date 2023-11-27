param ([string] $version)

$path = "/opt/octopus/tentacle$version/"

If(!(test-path -PathType container $path))
{
    apt-get install tentacle=$version
    cp -R "/opt/octopus/tentacle/" $path
}