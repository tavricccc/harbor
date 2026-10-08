#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$catalog = [IO.File]::ReadAllText((Join-Path $repo 'core/localization/en-US.json')) | ConvertFrom-Json -AsHashtable
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
