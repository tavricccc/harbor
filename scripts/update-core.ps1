#Requires -Version 7.0
param(
    [ValidateSet('stable', 'preview')][string]$Channel = 'preview',
    [string]$Version,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$headers = @{ 'User-Agent' = 'Harbor-core-updater' }
if ($Version) {
    $release = Invoke-RestMethod "https://api.github.com/repos/GopeedLab/gopeed/releases/tags/$([Uri]::EscapeDataString($Version))" -Headers $headers
} else {
    $releases = Invoke-RestMethod 'https://api.github.com/repos/GopeedLab/gopeed/releases?per_page=100' -Headers $headers
    $release = $releases | Where-Object { !$_.draft -and ($Channel -eq 'preview' -or !$_.prerelease) } | Select-Object -First 1
}
if (!$release -or $release.draft) { throw 'No published Gopeed release found' }
$tag = $release.tag_name
# Gopeed's v2 tags still declare a module path without /v2. Resolve the official
# tag, then let Go generate its standard pseudo-version instead of patching it.
$refs = @(git ls-remote https://github.com/GopeedLab/gopeed.git "refs/tags/$tag" "refs/tags/$tag^{}")
if ($LASTEXITCODE -or !$refs.Count) { throw "Cannot resolve Gopeed $tag" }
$peeled = $refs | Where-Object { $_.EndsWith('^{}') } | Select-Object -First 1
$revision = (($peeled ?? $refs[0]) -split '\s+')[0]
Push-Location (Join-Path $repo 'core')
try {
    go get "github.com/GopeedLab/gopeed@$revision"
    if ($LASTEXITCODE) { throw 'Gopeed dependency update failed' }
    go mod tidy
    if ($LASTEXITCODE) { throw 'Go module synchronization failed' }
    $module = go list -m -json github.com/GopeedLab/gopeed | ConvertFrom-Json
    if ($LASTEXITCODE) { throw 'Cannot read the resolved Gopeed version' }
    $metadata = [ordered]@{ release = $tag; channel = $(if ($release.prerelease) { 'preview' } else { 'stable' }); moduleVersion = $module.Version }
    [IO.File]::WriteAllText((Join-Path $repo 'core/upstream.json'), ($metadata | ConvertTo-Json) + "`n")
    Write-Host "Gopeed $tag ($($module.Version))"
} finally { Pop-Location }
if ($Test) { & (Join-Path $PSScriptRoot 'build.ps1') -Test }
