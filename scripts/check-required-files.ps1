# Verifies that the repository keeps its documented, user-facing files.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$required = @(
    'README.md',
    'README.de.md',
    'LICENSE',
    'CONTRIBUTING.md',
    'CODE_OF_CONDUCT.md',
    'SECURITY.md',
    'SUPPORT.md',
    'CHANGELOG.md',
    'CITATION.cff',
    'THIRD_PARTY_NOTICES.md',
    'PRIVACY.md',
    'DISCLAIMER.en.md',
    'DISCLAIMER.de.md',
    '.editorconfig',
    '.gitattributes',
    '.gitignore',
    '.github/PULL_REQUEST_TEMPLATE.md',
    '.github/ISSUE_TEMPLATE/bug.yml',
    '.github/ISSUE_TEMPLATE/feature.yml',
    'docs/architecture.md',
    'docs/security-model.md',
    'docs/tweak-model.md',
    'docs/roadmap.md',
    'docs/known-limitations.md',
    'docs/build.md',
    'docs/supported-features.md',
    'docs/sample-report.md'
)

$missing = foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $file))) {
        $file
    }
}

if ($missing) {
    Write-Output 'Missing required files:'
    $missing | ForEach-Object { Write-Output "  $_" }
    exit 1
}

Write-Output "All $($required.Count) required files are present."
