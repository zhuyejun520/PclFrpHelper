<#
.SYNOPSIS
    Builds PclFrpHelper (WinForms) and PclFrpAddon (WPF), optionally patching a PCL executable.

.DESCRIPTION
    Uses the .NET Framework csc.exe bundled with Windows by default, so no Visual Studio is required.
    Pass -CscPath to use Roslyn or another compiler.

.PARAMETER PclExe
    Path to an unmodified "Plain Craft Launcher 2.exe" (PCL 2.12.6.2). When supplied, the addon is
    embedded into a patched copy in dist\.

.PARAMETER CecilPath
    Path to Mono.Cecil.dll. Required when -PclExe is used.

.EXAMPLE
    ./build.ps1

.EXAMPLE
    ./build.ps1 -PclExe "C:\PCL\Plain Craft Launcher 2.exe" -CecilPath "C:\tools\Mono.Cecil.dll"
#>
[CmdletBinding()]
param(
    [string]$CscPath = '',
    [string]$PclExe = '',
    [string]$CecilPath = '',
    [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $root 'dist' }
$buildDir = Join-Path $root 'build'
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path $fw)) { $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$wpf = Join-Path $fw 'WPF'

function Resolve-Csc {
    if ($CscPath) { return (Resolve-Path $CscPath).Path }
    $candidates = @(
        (Join-Path $fw 'csc.exe')
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    throw 'csc.exe not found. Pass -CscPath explicitly.'
}

function Invoke-Csc {
    param([string[]]$Arguments)
    & $script:csc @Arguments
    if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
}

$csc = Resolve-Csc
Write-Host "Using compiler: $csc"

New-Item -ItemType Directory -Force -Path $buildDir, $OutDir, (Join-Path $OutDir 'PCL\frp') | Out-Null

$helperSources = @(
    (Join-Path $root 'src\PclFrpHelper\Program.cs'),
    (Join-Path $root 'src\PclFrpHelper\GlassControls.cs'),
    (Join-Path $root 'src\PclFrpHelper\MainForm.cs')
)
$helperExe = Join-Path $buildDir 'PclFrpHelper.exe'
$addonDll = Join-Path $buildDir 'PclFrpAddon.dll'

Write-Host '==> Building PclFrpHelper.exe'
$helperArgs = @(
    '/nologo', '/target:winexe', '/optimize+', "/out:$helperExe",
    "/r:$(Join-Path $fw 'System.dll')",
    "/r:$(Join-Path $fw 'System.Core.dll')",
    "/r:$(Join-Path $fw 'System.Windows.Forms.dll')",
    "/r:$(Join-Path $fw 'System.Drawing.dll')",
    "/r:$(Join-Path $fw 'System.Web.Extensions.dll')",
    "/r:$(Join-Path $fw 'System.IO.Compression.dll')",
    "/r:$(Join-Path $fw 'System.IO.Compression.FileSystem.dll')"
) + $helperSources
Invoke-Csc -Arguments $helperArgs

Write-Host '==> Building PclFrpAddon.dll'
$addonArgs = @(
    '/nologo', '/target:library', '/optimize+', "/out:$addonDll",
    "/r:$(Join-Path $wpf 'PresentationFramework.dll')",
    "/r:$(Join-Path $wpf 'PresentationCore.dll')",
    "/r:$(Join-Path $wpf 'WindowsBase.dll')",
    "/r:$(Join-Path $fw 'System.Xaml.dll')",
    "/r:$(Join-Path $fw 'System.dll')",
    "/r:$(Join-Path $fw 'System.Core.dll')",
    "/resource:$helperExe,PclFrpHelper.exe"
) + @((Join-Path $root 'src\PclFrpAddon\Ui.cs'))
Invoke-Csc -Arguments $addonArgs

Copy-Item $helperExe (Join-Path $OutDir 'PCL\frp\PclFrpHelper.exe') -Force
Copy-Item $addonDll (Join-Path $OutDir 'PCL\frp\PclFrpAddon.dll') -Force
Write-Host "==> Artifacts written to $OutDir\PCL\frp"

if ($PclExe) {
    if (-not $CecilPath) { throw '-CecilPath is required when -PclExe is supplied.' }
    $patched = Join-Path $OutDir 'Plain Craft Launcher 2.exe'
    Write-Host '==> Patching PCL executable'
    & (Join-Path $root 'tools\patch-pcl.ps1') -InputExe $PclExe -AddonDll $addonDll -OutputExe $patched -CecilPath $CecilPath
    if ($LASTEXITCODE -ne 0) { throw "patch-pcl.ps1 failed with exit code $LASTEXITCODE" }
    Write-Host "==> Patched launcher: $patched"
}

Write-Host 'Done.'
