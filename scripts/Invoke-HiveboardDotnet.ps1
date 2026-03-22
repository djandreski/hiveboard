$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'

& (Join-Path $PSScriptRoot 'Initialize-SandboxDotnet.ps1')

function Invoke-DotnetCommand {
    param([string[]] $Arguments)

    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if ($args.Count -eq 0) {
    & $dotnet
    exit $LASTEXITCODE
}

$command = $args[0]
$remainingArguments = @($args | Select-Object -Skip 1)
$targetPaths = @()

for ($index = 0; $index -lt $remainingArguments.Count; $index++) {
    $argument = $remainingArguments[$index]
    if ($argument.StartsWith('-') -or $argument.StartsWith('/')) {
        continue
    }

    if ($argument.EndsWith('.csproj', [System.StringComparison]::OrdinalIgnoreCase) -or
        $argument.EndsWith('.sln', [System.StringComparison]::OrdinalIgnoreCase)) {
        $targetPaths += $argument
    }
}

$sharedArguments = foreach ($argument in $remainingArguments) {
    if ($targetPaths -contains $argument) {
        continue
    }

    $argument
}

$solutionTarget = 'Hiveboard.sln'
$apiProjectTarget = 'src/Hiveboard.Api/Hiveboard.Api.csproj'
$coreProjectTarget = 'src/Hiveboard.Core/Hiveboard.Core.csproj'
$infrastructureProjectTarget = 'src/Hiveboard.Infrastructure/Hiveboard.Infrastructure.csproj'
$sandboxApiOutputPath = Join-Path $repoRoot 'src\Hiveboard.Api\bin\Sandbox\'
$sandboxApiIntermediatePath = Join-Path $repoRoot 'src\Hiveboard.Api\obj\Sandbox\'
$defaultToHiveboardBuild = $command -eq 'build' -and $targetPaths.Count -eq 0
$isSolutionBuild = $command -eq 'build' -and ($targetPaths -contains $solutionTarget)
$isApiBuild = $command -eq 'build' -and ($targetPaths -contains $apiProjectTarget)

if ($defaultToHiveboardBuild -or $isSolutionBuild -or $isApiBuild) {
    $restoreRequested = -not ($sharedArguments -contains '--no-restore')
    $restoreArguments = @('restore', '--ignore-failed-sources', '-p:NuGetAudit=false') + $sharedArguments
    $coreBuildArguments = @('build', $coreProjectTarget, '--no-restore') + $sharedArguments
    $infrastructureBuildArguments = @('build', $infrastructureProjectTarget, '--no-restore') + $sharedArguments
    $apiRestoreArguments = @(
        'restore',
        $apiProjectTarget,
        '--ignore-failed-sources',
        '-p:NuGetAudit=false',
        '-p:UseSandboxBuildWorkaround=true',
        '-p:BuildDashboardAssets=false',
        "-p:BaseOutputPath=$sandboxApiOutputPath",
        "-p:BaseIntermediateOutputPath=$sandboxApiIntermediatePath",
        "-p:MSBuildProjectExtensionsPath=$sandboxApiIntermediatePath"
    ) + $sharedArguments
    $apiBuildArguments = @(
        'build',
        $apiProjectTarget,
        '--no-restore',
        '-p:UseSandboxBuildWorkaround=true',
        '-p:BuildDashboardAssets=false',
        "-p:BaseOutputPath=$sandboxApiOutputPath",
        "-p:BaseIntermediateOutputPath=$sandboxApiIntermediatePath",
        "-p:MSBuildProjectExtensionsPath=$sandboxApiIntermediatePath"
    ) + $sharedArguments

    if ($restoreRequested) {
        Invoke-DotnetCommand ($restoreArguments + $coreProjectTarget)
        Invoke-DotnetCommand ($restoreArguments + $infrastructureProjectTarget)
        Invoke-DotnetCommand $apiRestoreArguments
    }

    Invoke-DotnetCommand $coreBuildArguments
    Invoke-DotnetCommand $infrastructureBuildArguments
    Invoke-DotnetCommand $apiBuildArguments
    exit 0
}

Invoke-DotnetCommand $args
