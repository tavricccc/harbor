# Shared language metadata and catalogs used by checks.
function Get-HarborLanguages {
    param([string]$Repository)
    [IO.File]::ReadAllText((Join-Path $Repository 'localization/languages.json')) | ConvertFrom-Json
}

function Get-HarborCatalog {
    param([string]$Repository, [string]$Language)
    [IO.File]::ReadAllText((Join-Path $Repository "localization/$Language.json")) | ConvertFrom-Json -AsHashtable
}
