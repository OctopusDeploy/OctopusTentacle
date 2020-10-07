[CmdletBinding()]
Param(
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$ScriptArgs
)

& dotnet tool restore
& dotnet tool run dotnet-cake --bootstrap --verbosity=Diagnostic
& dotnet tool run dotnet-cake $ScriptArgs
exit $LASTEXITCODE
