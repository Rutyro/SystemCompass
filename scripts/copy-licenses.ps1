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
    @{ Source = 'dirkster.avalondock/5.0.1/LICENSE'; Name = 'Ms-PL.txt' }
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
