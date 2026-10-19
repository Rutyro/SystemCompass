# Copies third-party license texts into a published artifact.
param(
    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
New-Item -ItemType Directory -Force -Path $Destination | Out-Null

$licenses = @(
    @{ Source = 'microsoft.windowsappsdk/2.5.1/license.txt'; Name = 'Microsoft.WindowsAppSDK.txt' },
    @{ Source = 'microsoft.web.webview2/1.0.3719.77/LICENSE.txt'; Name = 'Microsoft.WebView2.txt' },
    @{ Source = 'microsoft.windows.sdk.buildtools.msix/1.7.251221100/sdk_license.txt'; Name = 'Microsoft.Windows.SDK.BuildTools.txt' }
)

foreach ($license in $licenses) {
    $source = Join-Path $nugetRoot $license.Source
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $Destination $license.Name) -Force
    }
    else {
        Write-Output "warning: license file not found: $source"
    }
}

Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $Destination 'SystemCompass-MIT.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'THIRD_PARTY_NOTICES.md') -Destination $Destination -Force
Write-Output "Licenses copied to $Destination"
