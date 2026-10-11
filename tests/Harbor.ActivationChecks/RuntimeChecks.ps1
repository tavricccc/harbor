#Requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$ApplicationDirectory,
    [Parameter(Mandatory)][string]$MissingDotNetDirectory
)
$ErrorActionPreference = 'Stop'
$harborDirectory = [IO.Path]::GetFullPath($ApplicationDirectory)
$missingDirectory = [IO.Path]::GetFullPath($MissingDotNetDirectory)

function Invoke-HostCheck {
    param([string]$Directory, [string]$Argument, [string]$DotNetRoot)
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $Directory 'Harbor.exe'))
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardError = $true
    $start.Environment['DOTNET_DISABLE_GUI_ERRORS'] = '1'
    $start.ArgumentList.Add($Argument)
    if ($DotNetRoot) {
        $start.Environment['DOTNET_ROOT_X64'] = $DotNetRoot
        $start.Environment['DOTNET_ROOT'] = $DotNetRoot
    }
    $child = [Diagnostics.Process]::Start($start)
    try {
        if (!$child.WaitForExit(10000)) { $child.Kill(); throw "$Argument timed out" }
        [pscustomobject]@{ Code = $child.ExitCode; Error = $child.StandardError.ReadToEnd() }
    } finally { $child.Dispose() }
}

foreach ($argument in @('--check-dotnet', '--check-runtime')) {
    $result = Invoke-HostCheck $harborDirectory $argument
    if ($result.Code -ne 0) { throw "$argument failed: $($result.Code) $($result.Error)" }
    Write-Host "PASS: $argument"
}

# The fixture is published with AppHostDotNetSearch=EnvironmentVariable, so it
# cannot discover the host machine's global runtime when given an empty root.
$emptyRoot = Join-Path ([IO.Path]::GetTempPath()) ('Harbor-MissingDotNet-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($emptyRoot) | Out-Null
try {
    $result = Invoke-HostCheck $missingDirectory '--check-dotnet' $emptyRoot
    if ($result.Code -eq 0 -or $result.Error -notmatch 'You must install .NET') {
        throw "Missing .NET was not reported: $($result.Error)"
    }
    Write-Host "PASS: missing .NET produces the host's installation message ($($result.Code))"
} finally { [IO.Directory]::Delete($emptyRoot) }

$native = [Runtime.InteropServices.NativeLibrary]::Load((Join-Path $harborDirectory 'Microsoft.WindowsAppRuntime.Bootstrap.dll'))
try {
    [Reflection.Assembly]::LoadFrom((Join-Path $harborDirectory 'Microsoft.WindowsAppRuntime.Bootstrap.Net.dll')) | Out-Null
    $application = [Reflection.Assembly]::LoadFrom((Join-Path $harborDirectory 'Harbor.dll'))
    $release = $application.GetType('Microsoft.WindowsAppSDK.Release')
    $majorMinor = $release.GetField('MajorMinor', [Reflection.BindingFlags]'Static,NonPublic').GetRawConstantValue()
    $errorCode = 0
    $found = [Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap]::TryInitialize(
        $majorMinor, '', [Microsoft.Windows.ApplicationModel.DynamicDependency.PackageVersion]::new(([uint64]99 -shl 48)),
        [Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap+InitializeOptions]::None, [ref]$errorCode)
    if ($found -or $errorCode -ge 0) { throw 'Unavailable Windows App Runtime version was not rejected' }
    Write-Host "PASS: unavailable Windows App Runtime returns failure ($errorCode), without a UI or registry changes"
} finally { [Runtime.InteropServices.NativeLibrary]::Free($native) }
