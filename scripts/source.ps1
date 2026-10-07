#Requires -Version 7.0
param([string]$Version)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'core-module.ps1')
$upstream = Get-GopeedModule $repo
if (!$Version) {
    $project = [xml][IO.File]::ReadAllText((Join-Path $repo 'src/Harbor/Harbor.csproj'))
    $Version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
}
$work = [IO.Path]::GetFullPath((Join-Path $repo 'work'))
$stage = Join-Path $work ('source-' + [Guid]::NewGuid().ToString('N'))
$source = Join-Path $stage 'source'
$output = Join-Path $repo "artifacts/Harbor-Source-$Version.zip"
[IO.Directory]::CreateDirectory($stage) | Out-Null
try {
    $nativeArchive = Join-Path $stage 'native.zip'
    git -C $repo archive HEAD --format=zip "--output=$nativeArchive"
    if ($LASTEXITCODE) { throw 'Native source archive failed' }
    Expand-Archive -LiteralPath (Join-Path $stage 'native.zip') -DestinationPath $source
    $vendor = Join-Path $source 'third_party/gopeed'
    [IO.Directory]::CreateDirectory($vendor) | Out-Null
    Copy-Item -Path (Join-Path $upstream.Directory '*') -Destination $vendor -Recurse -Force
    # workflow_dispatch can update the dependency before packaging. Include the
    # tested module state even when it differs from the checked-out Git revision.
    foreach ($file in @('go.mod', 'go.sum', 'upstream.json')) {
        Copy-Item -LiteralPath (Join-Path $repo "core/$file") -Destination (Join-Path $source "core/$file") -Force
    }
    $revision = git -C $repo rev-parse HEAD
    [IO.File]::WriteAllText((Join-Path $source 'SOURCE-REVISION.txt'), "Harbor: $revision`nGopeed release: $($upstream.Release)`nGo module: $($upstream.Version)`nModule checksum: $($upstream.Sum)`n")
    Compress-Archive -Path (Join-Path $source '*') -DestinationPath $output -Force
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($output)
    try {
        $names = @($archive.Entries.FullName | ForEach-Object { $_.Replace('\','/') })
        foreach ($required in @('.github/workflows/windows.yml','core/cmd/browser-host/main.go','core/upstream.json','scripts/update-core.ps1','scripts/installer.iss','third_party/gopeed/LICENSE','third_party/gopeed/go.mod','SOURCE-REVISION.txt')) {
            if ($names -notcontains $required) { throw "Missing source: $required" }
        }
        if ($names | Where-Object { $_ -match '(^|/)(bin|obj|work|artifacts|\.git)/|\.(db|pfx)$' }) { throw 'Generated or private files found in source archive' }
        Write-Host "Source archive verified: $($archive.Entries.Count) entries"
    } finally { $archive.Dispose() }
} finally {
    $resolved = [IO.Path]::GetFullPath($stage)
    if (!$resolved.StartsWith($work + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected source staging path' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
