[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [string[]] $Arguments = @()
)

$ErrorActionPreference = 'Stop'
$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$SolutionPath = Join-Path $RepositoryRoot 'wv2wall\wv2wall.sln'
$DotnetArguments = @('test', $SolutionPath, '--configuration', $Configuration) + $Arguments

Push-Location $RepositoryRoot
try {
    & dotnet @DotnetArguments
    $ExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $ExitCode
