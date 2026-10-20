# RebornMenu v0.1 experimental local build and optional test-instance install.
# GitHub remains source of truth; game references never get committed.
# Examples:
#   .\build.ps1
#   .\build.ps1 -GameDir "$env:LOCALAPPDATA\OrbusRebornModdingInstances\My Orbus Renewed" -Install

[CmdletBinding()]
param(
    [string] $GameDir = "",
    [switch] $Install
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root 'src\OrbusRebornMenu\OrbusRebornMenu.csproj'
$output = Join-Path $root 'src\OrbusRebornMenu\bin\Release\net6.0\OrbusRebornMenu.dll'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET 8 SDK is required to build RebornMenu."
}

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $library = Join-Path $env:LOCALAPPDATA 'OrbusRebornModdingInstances'
    if (-not (Test-Path $library)) {
        throw "No RM instance library found. Supply -GameDir <modded game folder>."
    }
    $candidates = @(Get-ChildItem -LiteralPath $library -Directory |
        Where-Object {
            Test-Path (Join-Path $_.FullName 'BepInEx\interop\Il2Cppmscorlib.dll') -PathType Leaf
        })
    if ($candidates.Count -eq 0) {
        throw "No BepInEx interop found. Launch a prepared modded instance once or specify -GameDir."
    }
    if ($candidates.Count -eq 1) {
        $GameDir = $candidates[0].FullName
    } else {
        Write-Host 'Select a game instance used for COMPILING RebornMenu:' -ForegroundColor Cyan
        for ($i = 0; $i -lt $candidates.Count; $i++) {
            Write-Host "  [$($i+1)] $($candidates[$i].Name)"
        }
        $selected = Read-Host 'Instance number'
        $number = 0
        if (-not [int]::TryParse($selected, [ref]$number) -or
            $number -lt 1 -or $number -gt $candidates.Count) {
            throw "Invalid instance number."
        }
        $GameDir = $candidates[$number - 1].FullName
    }
}

$GameDir = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($GameDir.Trim('"')))
$required = @(
    'BepInEx\core\BepInEx.Core.dll',
    'BepInEx\core\BepInEx.Unity.IL2CPP.dll',
    'BepInEx\core\Il2CppInterop.Runtime.dll',
    'BepInEx\interop\Il2Cppmscorlib.dll',
    'BepInEx\interop\UnityEngine.CoreModule.dll',
    'BepInEx\interop\UnityEngine.TextRenderingModule.dll',
    'BepInEx\interop\UnityEngine.InputModule.dll',
    'BepInEx\interop\UnityEngine.PhysicsModule.dll'
)
foreach ($item in $required) {
    if (-not (Test-Path (Join-Path $GameDir $item) -PathType Leaf)) {
        throw "Missing $item. Run the modded game once before building."
    }
}
if (-not (Test-Path (Join-Path $GameDir 'BepInEx\interop\UnityEngine.VRModule.dll')) -and
    -not (Test-Path (Join-Path $GameDir 'BepInEx\interop\UnityEngine.XRModule.dll'))) {
    throw "No Unity XR or VR interop. The selected game's BepInEx setup is incomplete."
}

Write-Host "Building RebornMenu v0.1 from $GameDir" -ForegroundColor Cyan
& dotnet build $project --configuration Release --no-incremental "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $output)) {
    throw "RebornMenu compilation failed. Do NOT attempt to install."
}
Write-Host "Compiled DLL: $output" -ForegroundColor Green

if ($Install) {
    $running = @(Get-Process -Name 'vrclient','OrbusVR' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) { throw "Close ALL OrbusVR clients before copying a test plugin." }

    $pluginFolder = Join-Path $GameDir 'BepInEx\plugins\OrbusRebornMenu'
    New-Item -ItemType Directory -Force -Path $pluginFolder | Out-Null
    $dll = Join-Path $pluginFolder 'OrbusRebornMenu.dll'
    if (Test-Path $dll) {
        $backup = $dll + '.previous-test'
        Copy-Item -LiteralPath $dll -Destination $backup -Force
        Write-Host "Previous test build saved: $backup" -ForegroundColor Yellow
    }
    Copy-Item -LiteralPath $output -Destination $dll -Force
    Write-Host "Installed to ONLY the chosen test instance: $dll" -ForegroundColor Green
    Write-Host "Launch from RM. Press CTRL+ALT+M and inspect BepInEx\LogOutput.log." -ForegroundColor Cyan
} else {
    Write-Host 'This command only built the DLL. Use -Install to copy to one selected test instance.' -ForegroundColor Gray
}
