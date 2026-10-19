# Fails when the repository contains binaries or strings from third-party
# optimization packs. Source code and tweak definitions must be original.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $tracked = @()
    if (Test-Path '.git') {
        $tracked = & git ls-files 2>$null
    }

    if (-not $tracked) {
        $tracked = Get-ChildItem -Recurse -File |
            Where-Object {
                $_.FullName -notmatch '\\\.git\\' -and
                $_.FullName -notmatch '\\bin\\' -and
                $_.FullName -notmatch '\\obj\\'
            } |
            ForEach-Object { Resolve-Path -Relative $_.FullName }
    }

    $binaryExtensions = @('.exe', '.dll', '.sys', '.rar', '.7z', '.zip', '.pdb', '.msi', '.reg', '.spg')
    $binaryFiles = $tracked | Where-Object {
        $binaryExtensions -contains [IO.Path]::GetExtension($_).ToLowerInvariant()
    }

    $terms = @(
        ('fr' + '33thy'),
        ('Everything' + 'Tech'),
        ('Karan' + 'PC'),
        ('Mel' + 'ody'),
        ('Scy' + 'ta'),
        ('Win' + 'aero'),
        ('Process' + '.Lasso')
    )

    $textExtensions = @('.cs', '.xaml', '.json', '.md', '.ps1', '.yml', '.yaml', '.props', '.csproj', '.slnx', '.txt', '.cff')
    $hits = New-Object System.Collections.Generic.List[string]
    foreach ($file in $tracked) {
        if ($file -like 'scripts/*') { continue }
        if ($textExtensions -notcontains [IO.Path]::GetExtension($file).ToLowerInvariant()) { continue }
        $content = Get-Content -LiteralPath $file -Raw -ErrorAction SilentlyContinue
        if ($null -eq $content) { continue }
        foreach ($term in $terms) {
            if ($content.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $hits.Add("$file contains a third-party marker")
            }
        }
    }

    if ($binaryFiles) {
        Write-Output 'Binary or archive files must not be committed:'
        $binaryFiles | ForEach-Object { Write-Output "  $_" }
    }

    if ($hits.Count -gt 0) {
        Write-Output 'Third-party markers found:'
        $hits | ForEach-Object { Write-Output "  $_" }
    }

    if ($binaryFiles -or $hits.Count -gt 0) {
        exit 1
    }

    Write-Output 'Clean-room check passed.'
}
finally {
    Pop-Location
}
