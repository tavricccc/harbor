#Requires -Version 7.0
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'core-module.ps1')
$upstream = Get-GopeedModule $repo
Copy-Item -LiteralPath (Join-Path $upstream.Directory 'LICENSE') -Destination (Join-Path $repo 'core/LICENSE-Gopeed.txt') -Force
$env:CGO_ENABLED = '0'
$env:TORRENT_STORAGE_DEFAULT_FILE_IO = 'classic'
Push-Location (Join-Path $repo 'core')
try {
    $coreVersion = $upstream.Release.TrimStart('v')
    go build -trimpath -ldflags "-s -w -H=windowsgui -X github.com/GopeedLab/gopeed/pkg/base.Version=$coreVersion" -o harbor-core.exe .
    if ($LASTEXITCODE) { throw 'Go core build failed' }
    go build -trimpath -ldflags '-s -w -H=windowsgui' -o harbor-browser-host.exe ./cmd/browser-host
    if ($LASTEXITCODE) { throw 'Browser host build failed' }
    if ($Test) {
        go mod verify
        if ($LASTEXITCODE) { throw 'Go module checksum verification failed' }
        go test ./...
        if ($LASTEXITCODE) { throw 'Core verification failed' }
    }
} finally { Pop-Location }
if ($Test) {
    dotnet run --project (Join-Path $repo 'tests/Harbor.ProtocolChecks/ProtocolCheck.csproj')
    if ($LASTEXITCODE) { throw 'Gopeed protocol verification failed' }
}
$app = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/app'))
if ($app -ne "$repo\artifacts\app") { throw 'Unexpected publish path' }
if (Test-Path -LiteralPath $app) { Get-ChildItem -LiteralPath $app -Force | Remove-Item -Recurse -Force }
dotnet publish (Join-Path $repo 'src/Harbor/Harbor.csproj') -c Release -r win-x64 -p:Platform=x64 -o $app
if ($LASTEXITCODE) { throw 'WinUI publish failed' }
foreach ($asset in @('Assets/AppIcon.ico', 'Assets/Square44x44Logo.scale-200.png')) {
    if (!(Test-Path -LiteralPath (Join-Path $app $asset))) { throw "Missing published icon: $asset" }
}
