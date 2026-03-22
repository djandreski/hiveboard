$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sdkVersion = '10.0.201'
$programFilesDotnet = Join-Path $env:ProgramFiles 'dotnet'
$sourceSdkRoot = Join-Path $programFilesDotnet "sdk\$sdkVersion"

if (-not (Test-Path $sourceSdkRoot)) {
    throw "Expected .NET SDK $sdkVersion at '$sourceSdkRoot'."
}

function Ensure-Directory {
    param([string] $Path)

    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Ensure-Junction {
    param(
        [string] $Path,
        [string] $Target
    )

    if (Test-Path $Path) {
        return
    }

    New-Item -ItemType Junction -Path $Path -Target $Target | Out-Null
}

$sandboxRoot = Join-Path $repoRoot '.sandbox-dotnet-root'
$sandboxSdkRoot = Join-Path $sandboxRoot "sdk\$sdkVersion"
$sandboxSdksRoot = Join-Path $sandboxSdkRoot 'Sdks'
$sandboxAppDataRoaming = Join-Path $repoRoot '.appdata\Roaming'
$sandboxAppDataLocal = Join-Path $repoRoot '.appdata\Local'
$sandboxNuGetConfig = Join-Path $sandboxAppDataRoaming 'NuGet\NuGet.Config'

Ensure-Directory $sandboxSdksRoot

foreach ($name in @('packs', 'shared', 'host', 'metadata', 'sdk-manifests', 'templates')) {
    $sourcePath = Join-Path $programFilesDotnet $name
    if (Test-Path $sourcePath) {
        Ensure-Junction (Join-Path $sandboxRoot $name) $sourcePath
    }
}

foreach ($file in @('LICENSE.txt', 'ThirdPartyNotices.txt', 'dotnet.ico')) {
    $sourceFile = Join-Path $programFilesDotnet $file
    $destinationFile = Join-Path $sandboxRoot $file

    if ((Test-Path $sourceFile) -and -not (Test-Path $destinationFile)) {
        Copy-Item $sourceFile $destinationFile
    }
}

Get-ChildItem $sourceSdkRoot | ForEach-Object {
    if ($_.Name -eq 'Sdks') {
        return
    }

    $destinationPath = Join-Path $sandboxSdkRoot $_.Name
    if ($_.PSIsContainer) {
        Ensure-Junction $destinationPath $_.FullName
        return
    }

    if (-not (Test-Path $destinationPath)) {
        Copy-Item $_.FullName $destinationPath
    }
}

Get-ChildItem (Join-Path $sourceSdkRoot 'Sdks') -Directory | ForEach-Object {
    $destinationPath = Join-Path $sandboxSdksRoot $_.Name

    if ($_.Name -eq 'Microsoft.NET.Sdk') {
        if (-not (Test-Path $destinationPath)) {
            Copy-Item $_.FullName $destinationPath -Recurse
        }

        $supportedTfmsFile = Join-Path $destinationPath 'targets\Microsoft.NET.SupportedTargetFrameworks.props'
        $supportedTfmsContent = Get-Content $supportedTfmsFile -Raw

        if ($supportedTfmsContent.Contains('$([MSBuild]::Add($(NETCoreAppMaximumVersion), 1)).0')) {
            $supportedTfmsContent = $supportedTfmsContent.Replace(
                '$([MSBuild]::Add($(NETCoreAppMaximumVersion), 1)).0',
                '11.0')

            Set-Content $supportedTfmsFile $supportedTfmsContent -Encoding UTF8
        }

        return
    }

    Ensure-Junction $destinationPath $_.FullName
}

$autoImportSdkRoot = Join-Path $sandboxSdksRoot 'Microsoft.NET.SDK.WorkloadAutoImportPropsLocator\Sdk'
$manifestSdkRoot = Join-Path $sandboxSdksRoot 'Microsoft.NET.SDK.WorkloadManifestTargetsLocator\Sdk'

Ensure-Directory $autoImportSdkRoot
Ensure-Directory $manifestSdkRoot

foreach ($stubFile in @(
    (Join-Path $autoImportSdkRoot 'Sdk.props'),
    (Join-Path $autoImportSdkRoot 'Sdk.targets'),
    (Join-Path $autoImportSdkRoot 'AutoImport.props'),
    (Join-Path $manifestSdkRoot 'Sdk.props'),
    (Join-Path $manifestSdkRoot 'Sdk.targets'),
    (Join-Path $manifestSdkRoot 'WorkloadManifest.targets')
)) {
    if (-not (Test-Path $stubFile)) {
        Set-Content $stubFile '<Project />' -Encoding ASCII
    }
}

Ensure-Directory (Split-Path $sandboxNuGetConfig -Parent)
Copy-Item (Join-Path $repoRoot 'NuGet.Config') $sandboxNuGetConfig -Force

$env:APPDATA = $sandboxAppDataRoaming
$env:LOCALAPPDATA = $sandboxAppDataLocal
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.dotnet'
$env:HOME = $env:DOTNET_CLI_HOME
$env:DOTNET_ROOT = $sandboxRoot
$env:NUGET_PACKAGES = Join-Path $env:USERPROFILE '.nuget\packages'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$env:MSBuildSDKsPath = $sandboxSdksRoot
$env:DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR = $sandboxSdksRoot
$env:DOTNET_MSBUILD_SDK_RESOLVER_SDKS_VER = $sdkVersion
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $sandboxRoot
