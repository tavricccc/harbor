#Requires -Version 7.0
param(
    [ValidateSet('stable', 'preview', 'main')][string]$Channel = 'preview',
    [string]$Version,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$headers = @{ 'User-Agent' = 'Harbor-core-updater' }

if ($Channel -eq 'main' -and !$Version) {
    $ref = 'main'
    $sourceChannel = 'main'
    $refSpecs = @('refs/heads/main')
} else {
    if ($Version) {
        $release = Invoke-RestMethod "https://api.github.com/repos/GopeedLab/gopeed/releases/tags/$([Uri]::EscapeDataString($Version))" -Headers $headers
    } else {
        $releases = Invoke-RestMethod 'https://api.github.com/repos/GopeedLab/gopeed/releases?per_page=100' -Headers $headers
        $release = $releases | Where-Object { !$_.draft -and ($Channel -eq 'preview' -or !$_.prerelease) } | Select-Object -First 1
    }
    if (!$release -or $release.draft) { throw 'No published Gopeed release found' }
    $ref = $release.tag_name
    $sourceChannel = if ($release.prerelease) { 'preview' } else { 'stable' }
    $refSpecs = @("refs/tags/$ref", "refs/tags/$ref^{}")
}

# Resolve the official ref; Go records v2 tags without a /v2 module path as pseudo-versions.
$refs = @(git ls-remote https://github.com/GopeedLab/gopeed.git @refSpecs)
if ($LASTEXITCODE -or !$refs.Count) { throw "Cannot resolve Gopeed $ref" }
$peeled = $refs | Where-Object { $_.EndsWith('^{}') } | Select-Object -First 1
$revision = (($peeled ?? $refs[0]) -split '\s+')[0]

Push-Location $repo
try {
    go get "github.com/GopeedLab/gopeed@$revision"
    if ($LASTEXITCODE) { throw 'Gopeed dependency update failed' }
    go mod tidy
    if ($LASTEXITCODE) { throw 'Go module synchronization failed' }
    $module = go list -m -json github.com/GopeedLab/gopeed | ConvertFrom-Json
    if ($LASTEXITCODE) { throw 'Cannot read the resolved Gopeed version' }
    $metadata = [ordered]@{ release = $ref; channel = $sourceChannel; moduleVersion = $module.Version; revision = $revision }
    [IO.File]::WriteAllText((Join-Path $repo 'core/upstream.json'), ($metadata | ConvertTo-Json) + "`n")
    Write-Host "Gopeed $ref ($($module.Version))"
} finally { Pop-Location }
if ($Test) { & (Join-Path $PSScriptRoot 'build.ps1') -Test }
