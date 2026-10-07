# Shared source and version information for build/package scripts.
function Get-GopeedModule {
    param([string]$Repository)
    Push-Location (Join-Path $Repository 'core')
    try {
        $module = go list -m -json github.com/GopeedLab/gopeed | ConvertFrom-Json
        if ($LASTEXITCODE) { throw 'Cannot resolve the Gopeed module' }
        $source = go mod download -json "$($module.Path)@$($module.Version)" | ConvertFrom-Json
        if ($LASTEXITCODE) { throw 'Cannot download the Gopeed source' }
        $release = [IO.File]::ReadAllText((Join-Path $Repository 'core/upstream.json')) | ConvertFrom-Json
        if ($release.moduleVersion -ne $module.Version) { throw 'Run scripts/update-core.ps1 to synchronize the Gopeed release metadata' }
        [pscustomobject]@{
            Release = $release.release
            Version = $module.Version
            Directory = $source.Dir
            Zip = $source.Zip
            Sum = $source.Sum
        }
    } finally { Pop-Location }
}
