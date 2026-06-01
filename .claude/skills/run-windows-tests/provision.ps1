# In-guest provisioning, run once at first logon by autounattend.xml (output -> C:\provision.log).
# Installs the virtio-net driver so the network comes up, then OpenSSH + your key + .NET 8 SDK
# + rsync. Can also be run by hand:  provision.ps1 -PublicKey "ssh-ed25519 AAAA..."
#
# Leaves the OpenSSH default shell as cmd.exe on purpose: rsync-over-SSH into Windows is
# reliable with cmd, flaky with PowerShell. run.sh invokes dotnet via `powershell -Command`.

[CmdletBinding()]
param(
  [string] $PublicKey,
  [string] $PublicKeyFile
)
$ErrorActionPreference = "Stop"
function Log($m) { Write-Host "[provision] $m" }

if (-not $PublicKey -and $PublicKeyFile -and (Test-Path $PublicKeyFile)) {
  $PublicKey = (Get-Content -Raw $PublicKeyFile).Trim()
}
if (-not $PublicKey) { throw "No public key (pass -PublicKey or -PublicKeyFile)." }

# --- 1. virtio-net driver (brings the NIC online) ---------------------------
Log "Installing virtio-net driver from the virtio-win CD"
$inf = Get-PSDrive -PSProvider FileSystem | ForEach-Object {
  Get-ChildItem -Path $_.Root -Recurse -Filter netkvm.inf -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'ARM64' }
} | Select-Object -First 1
if ($inf) {
  pnputil /add-driver $inf.FullName /install
} else {
  Log "WARNING: netkvm.inf (ARM64) not found on any CD — network may be down."
}

Log "Waiting for network"
for ($i = 0; $i -lt 60; $i++) {
  if (Test-Connection -ComputerName 8.8.8.8 -Count 1 -Quiet) { break }
  Start-Sleep -Seconds 2
}

# --- 2. OpenSSH Server + your key -------------------------------------------
Log "Enabling OpenSSH Server"
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 | Out-Null
Set-Service -Name sshd -StartupType Automatic
Start-Service sshd

Log "Authorizing public key (admin user -> administrators_authorized_keys)"
$adminKeys = "C:\ProgramData\ssh\administrators_authorized_keys"
Set-Content -Path $adminKeys -Value $PublicKey -Encoding ascii
icacls $adminKeys /inheritance:r | Out-Null
icacls $adminKeys /grant "Administrators:F" "SYSTEM:F" | Out-Null

if (-not (Get-NetFirewallRule -Name sshd -ErrorAction SilentlyContinue)) {
  New-NetFirewallRule -Name sshd -DisplayName "OpenSSH Server (sshd)" `
    -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22 | Out-Null
}

# --- 3. Toolchain: .NET 8 SDK, Git, rsync -----------------------------------
Log "Installing .NET 8 SDK + Git via winget"
winget install --silent --accept-source-agreements --accept-package-agreements --id Microsoft.DotNet.SDK.8
winget install --silent --accept-source-agreements --accept-package-agreements --id Git.Git

if (-not (Get-Command rsync -ErrorAction SilentlyContinue)) {
  Log "Installing Chocolatey + rsync"
  Set-ExecutionPolicy Bypass -Scope Process -Force
  [System.Net.ServicePointManager]::SecurityProtocol = 3072
  iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
  choco install rsync -y
}

Restart-Service sshd
Log "Provisioning complete."
