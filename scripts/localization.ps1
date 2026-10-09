# Shared language metadata and catalogs used by checks and installer packaging.
function Get-HarborLanguages {
    param([string]$Repository)
    [IO.File]::ReadAllText((Join-Path $Repository 'localization/languages.json')) | ConvertFrom-Json
}

function Get-HarborCatalog {
    param([string]$Repository, [string]$Language)
    [IO.File]::ReadAllText((Join-Path $Repository "localization/$Language.json")) | ConvertFrom-Json -AsHashtable
}

function Write-InstallerLocalization {
    param([string]$Repository)
    $languages = @(Get-HarborLanguages $Repository)
    $ordered = @($languages | Where-Object { $_.id -eq 'en-US' }) + @($languages | Where-Object { $_.id -ne 'en-US' })
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('[Languages]')
    foreach ($language in $ordered) {
        $lines.Add(('Name: "{0}"; MessagesFile: "{1}"' -f $language.installerId, $language.installerMessages))
    }
    $lines.Add('')
    $lines.Add('[CustomMessages]')
    foreach ($language in $ordered) {
        $catalog = Get-HarborCatalog $Repository $language.id
        foreach ($key in @($catalog.Keys | Where-Object { $_.StartsWith('Installer.') } | Sort-Object)) {
            $value = $catalog[$key].Replace("`r`n", '%n').Replace("`n", '%n')
            $lines.Add(('{0}.{1}={2}' -f $language.installerId, $key.Substring('Installer.'.Length), $value))
        }
    }
    [IO.File]::WriteAllLines((Join-Path $Repository 'artifacts/installer-localization.iss'), $lines, [Text.UTF8Encoding]::new($true))
}
