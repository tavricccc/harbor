#Requires -Version 7.0
param([switch]$SkipBuild, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
$project = [xml][IO.File]::ReadAllText((Join-Path $repo 'src/Harbor/Harbor.csproj'))
$version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
$artifacts = Join-Path $repo 'artifacts'
$payload = Join-Path $artifacts 'app'
$manifest = Join-Path $payload 'installed-files.txt'
$files = @(Get-ChildItem -LiteralPath $payload -File -Recurse | Where-Object { $_.FullName -ne $manifest -and $_.Extension -ne '.pdb' } | ForEach-Object {
    [IO.Path]::GetRelativePath($payload, $_.FullName).ToLowerInvariant()
}) + 'installed-files.txt'
[IO.File]::WriteAllLines($manifest, @($files | Sort-Object), [Text.UTF8Encoding]::new($false))

# The setup embeds the app as one zip and installs it with the files listed in the manifest.
$zip = Join-Path $artifacts 'payload.zip'
$staging = Join-Path $artifacts 'payload'
Remove-Item -LiteralPath $zip, $staging -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item -LiteralPath $payload -Destination $staging -Recurse
Get-ChildItem -LiteralPath $staging -Recurse -Filter *.pdb | Remove-Item
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -CompressionLevel Optimal
Remove-Item -LiteralPath $staging -Recurse -Force

$setup = Join-Path $artifacts 'setup'
Remove-Item -LiteralPath $setup -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $repo 'src/HarborSetup/HarborSetup.csproj') -c Release -r win-x64 -p:Platform=x64 "-p:Version=$version" "-p:PayloadZip=$zip" -o $setup -nologo -v q
if ($LASTEXITCODE) { throw 'Setup publish failed' }
$loose = Get-ChildItem -LiteralPath $setup -File | Where-Object { $_.Name -ne 'HarborSetup.exe' -and $_.Extension -ne '.pdb' }
if ($loose) { throw "The setup is not a single file; also published: $($loose.Name -join ', ')" }
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { $artifacts }
New-Item -ItemType Directory -Force $output | Out-Null
$installer = Join-Path $output "Harbor-Setup-$version-x64.exe"
Move-Item -LiteralPath (Join-Path $setup 'HarborSetup.exe') -Destination $installer -Force
Remove-Item -LiteralPath $setup, $zip -Recurse -Force
# Harbor's updater downloads the setup only with this checksum beside it on the release.
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$installer.sha256", "$hash  $(Split-Path $installer -Leaf)`n", [Text.UTF8Encoding]::new($false))
Get-Item -LiteralPath $installer | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
