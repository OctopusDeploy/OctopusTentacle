$path = $PSScriptRoot
Write-Host "Applying permissions to directory $path"

# Remove inheritance but preserve existing entries
$acl = (Get-Item $path).GetAccessControl('Access')
$acl.SetAccessRuleProtection($true,$true)
Set-Acl  $path -AclObject $acl

# Remove write access rules for BUILTIN\Users
$acl = (Get-Item $path).GetAccessControl('Access')
$writeRules = $acl.Access | ? {($_.IdentityReference -eq 'BUILTIN\Users') -and ($_.AccessControlType -eq 'Allow')} | ? {($_.FileSystemRights -match 'CreateFiles') -or ($_.FileSystemRights -match 'AppendData')}
foreach ($rule in $writeRules) {
  $acl.RemoveAccessRule($rule)
}
Set-Acl $path -AclObject $acl