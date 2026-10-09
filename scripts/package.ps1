#Requires -Version 7.0
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
$project = [xml][IO.File]::ReadAllText((Join-Path $repo 'src/Harbor/Harbor.csproj'))
$version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
$payload = Join-Path $repo 'artifacts/app'
$manifest = Join-Path $payload 'installed-files.txt'
$files = @(Get-ChildItem -LiteralPath $payload -File -Recurse | Where-Object { $_.FullName -ne $manifest } | ForEach-Object {
    [IO.Path]::GetRelativePath($payload, $_.FullName).ToLowerInvariant()
}) + 'installed-files.txt'
[IO.File]::WriteAllLines($manifest, @($files | Sort-Object), [Text.UTF8Encoding]::new($false))
$compiler = Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = (Get-Command ISCC.exe -ErrorAction Stop).Source }
& $compiler /Q "/DAppVersion=$version" (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE) { throw 'Installer compilation failed' }
