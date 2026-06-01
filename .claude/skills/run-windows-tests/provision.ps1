# Provision a fresh Windows 11 ARM guest to run Octopus.Tentacle Windows tests over SSH.
# Run ONCE inside the VM, in an elevated PowerShell:
#
#   powershell -ExecutionPolicy Bypass -File provision.ps1 -PublicKey "ssh-ed25519 AAAA... you@mac"
#
# Pass the contents of your Mac's public key (e.g. ~/.ssh/id_ed25519.pub) as -PublicKey.
# Validate each step on first run — this is a scaffold, not battle-tested across builds.

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)] [string] $PublicKey,
  [string] $User = "dev"
)

$ErrorActionPreference = "Stop"

function Require-Admin {
  $p = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
  if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script in an elevated (Administrator) PowerShell."
  }
}
Require-Admin

Write-Host "==> Enabling OpenSSH Server" -ForegroundColor Cyan
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 | Out-Null
Set-Service -Name sshd -StartupType Automatic
Start-Service sshd
# Default shell PowerShell so `cd C:/repo && dotnet test ...` style commands work over ssh.
New-ItemProperty -Path "HKLM:\SOFTWARE\OpenSSH" -Name DefaultShell `
  -Value "C:\Program Files\PowerShell\7\pwsh.exe" -PropertyType String -Force -ErrorAction SilentlyContinue | Out-Null

Write-Host "==> Authorizing your SSH public key for '$User'" -ForegroundColor Cyan
# OpenSSH on Windows reads admins' keys from a single ACL-locked file.
$adminKeys = "C:\ProgramData\ssh\administrators_authorized_keys"
Set-Content -Path $adminKeys -Value $PublicKey -Encoding ascii
icacls $adminKeys /inheritance:r | Out-Null
icacls $adminKeys /grant "Administrators:F" "SYSTEM:F" | Out-Null

Write-Host "==> Allowing SSH through the firewall" -ForegroundColor Cyan
if (-not (Get-NetFirewallRule -Name sshd -ErrorAction SilentlyContinue)) {
  New-NetFirewallRule -Name sshd -DisplayName "OpenSSH Server (sshd)" `
    -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22 | Out-Null
}

Write-Host "==> Installing .NET 8 SDK, Git, and rsync via winget" -ForegroundColor Cyan
# winget IDs; rerun is idempotent. rsync is needed by run.sh's source sync.
winget install --silent --accept-source-agreements --accept-package-agreements --id Microsoft.DotNet.SDK.8
winget install --silent --accept-source-agreements --accept-package-agreements --id Git.Git
# rsync is not in winget's default catalog; install via Git's MSYS or Chocolatey.
if (-not (Get-Command rsync -ErrorAction SilentlyContinue)) {
  Write-Host "    rsync not found. Installing Chocolatey + rsync." -ForegroundColor Yellow
  Set-ExecutionPolicy Bypass -Scope Process -Force
  [System.Net.ServicePointManager]::SecurityProtocol = 3072
  iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
  choco install rsync -y
}

Write-Host "==> Restarting sshd to pick up DefaultShell" -ForegroundColor Cyan
Restart-Service sshd

Write-Host ""
Write-Host "Provisioning complete." -ForegroundColor Green
Write-Host "Verify from the Mac:  ssh -p 2222 $User@127.0.0.1 'dotnet --version; rsync --version | Select-Object -First 1'"
