$path = $PSScriptRoot

[System.Security.Principal.WellKnownSidType] $wellKnownSid = [System.Security.Principal.WellKnownSidType]::BuiltinUsersSid
$identifier = New-Object System.Security.Principal.SecurityIdentifier -ArgumentList $wellKnownSid, $null
$user = $identifier.Translate( [System.Security.Principal.NTAccount])

Write-Host "Removing write permissions for $user"

# Remove inheritance but preserve existing entries
$acl = (Get-Item $path).GetAccessControl('Access')
$acl.SetAccessRuleProtection($true,$true)
Set-Acl  $path -AclObject $acl

# Remove write access rules
$acl = (Get-Item $path).GetAccessControl('Access')
$writeRules = $acl.Access | ? {($_.IdentityReference -eq $user.Value) -and ($_.AccessControlType -eq 'Allow')} | ? {($_.FileSystemRights -match 'CreateFiles') -or ($_.FileSystemRights -match 'AppendData')}
foreach ($rule in $writeRules) {
  $acl.RemoveAccessRule($rule)
}
Set-Acl $path -AclObject $acl