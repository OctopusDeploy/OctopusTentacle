param (
  [Parameter(Mandatory=$false)]
  [string]$ProjectName="octopusdocker"
)

. ./Docker/Scripts/build-common.ps1
Confirm-RunningFromRootDirectory

TeamCity-Block("Stop and remove compose project") {
    Stop-DockerCompose $ProjectName .\Tentacle\docker-compose.yml
}
