#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'localization.ps1')
$catalog = Get-HarborCatalog $repo 'en-US'
$languages = Get-HarborLanguages $repo
foreach ($language in $languages) {
    $translation = Get-HarborCatalog $repo $language.id
    $difference = Compare-Object @($catalog.Keys | Sort-Object) @($translation.Keys | Sort-Object)
    if ($difference) { throw "Translation keys differ: $($language.id)" }
}
$references = 0
foreach ($file in @(git -C $repo ls-files 'src/Harbor/*.cs' 'src/Harbor/*.xaml' 'core/*.go')) {
    $source = [IO.File]::ReadAllText((Join-Path $repo $file))
    foreach ($match in [regex]::Matches($source, '(?:Strings\.(?:Get|Format)|localization\.Text)\("(?<key>[\w.]+)"|\{loc:Localize Key=(?<key>[\w.]+)\}')) {
        $key = $match.Groups['key'].Value
        if (!$catalog.ContainsKey($key)) { throw "Unknown localization key in ${file}: $key" }
        $references++
    }
}
Write-Host "Localization source references verified: $references"
