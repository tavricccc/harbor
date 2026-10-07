param([string]$Version)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
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
    $upstreamArchive = Join-Path $stage 'upstream.zip'
    git -C $repo archive HEAD --format=zip "--output=$nativeArchive"
    if ($LASTEXITCODE) { throw 'Native source archive failed' }
    Expand-Archive -LiteralPath (Join-Path $stage 'native.zip') -DestinationPath $source
    git -C (Join-Path $repo 'upstream') archive HEAD --format=zip "--output=$upstreamArchive"
    if ($LASTEXITCODE) { throw 'Upstream source archive failed' }
    Expand-Archive -LiteralPath (Join-Path $stage 'upstream.zip') -DestinationPath (Join-Path $source 'upstream')
    $revision = git -C $repo rev-parse HEAD
    $upstream = git -C (Join-Path $repo 'upstream') rev-parse HEAD
    [IO.File]::WriteAllText((Join-Path $source 'SOURCE-REVISION.txt'), "Harbor: $revision`nGopeed upstream: $upstream`n")
    Compress-Archive -Path (Join-Path $source '*') -DestinationPath $output -Force
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($output)
    try {
        $names = @($archive.Entries.FullName | ForEach-Object { $_.Replace('\','/') })
        foreach ($required in @('.github/workflows/windows.yml','core/cmd/browser-host/main.go','scripts/installer.iss','upstream/LICENSE','upstream/go.mod','SOURCE-REVISION.txt')) {
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
