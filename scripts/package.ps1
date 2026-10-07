param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
$compiler = Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = (Get-Command ISCC.exe -ErrorAction Stop).Source }
& $compiler /Q (Join-Path $PSScriptRoot 'installer.iss')
if ($LASTEXITCODE) { throw 'Installer compilation failed' }
