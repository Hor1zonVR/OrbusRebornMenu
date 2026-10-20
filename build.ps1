# RebornMenu v0.1 experimental compile-only helper; never installs in a game.
# GitHub remains source of truth; game references never get committed.
# Examples:
#   .\build.ps1
#   .\build.ps1 -GameDir "$env:LOCALAPPDATA\OrbusRebornModdingInstances\My Orbus Renewed"

[CmdletBinding()]
param(
    [string] $GameDir = ""
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root 'src\OrbusRebornMenu\OrbusRebornMenu.csproj'
$output = Join-Path $root 'src\OrbusRebornMenu\bin\Release\net6.0\OrbusRebornMenu.dll'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET 8 SDK is required to build RebornMenu."
}

# This selects compile REFERENCES, not an installation target.
# The resulting DLL is released to GitHub; players select the destination in RM.
$cachePath = Join-Path $root '.build-refs-path.txt'
function Test-ReferenceGame([string]$path) {
    return -not [string]::IsNullOrWhiteSpace($path) -and
           (Test-Path (Join-Path $path 'BepInEx\interop\Il2Cppmscorlib.dll') -PathType Leaf) -and
           (Test-Path (Join-Path $path 'BepInEx\interop\UnityEngine.InputModule.dll') -PathType Leaf)
}

if ([string]::IsNullOrWhiteSpace($GameDir) -and (Test-Path $cachePath -PathType Leaf)) {
    $cached = (Get-Content -LiteralPath $cachePath -Raw).Trim()
    if (Test-ReferenceGame $cached) {
        $GameDir = $cached
        Write-Host "Reusing saved build references: $GameDir" -ForegroundColor Cyan
    }
}
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $library = Join-Path $env:LOCALAPPDATA 'OrbusRebornModdingInstances'
    if (-not (Test-Path $library -PathType Container)) {
        throw "No RM instance library. Supply -GameDir once to a BepInEx-prepared game, or install an RM game instance."
    }

    # Prefer the previously verified Spades installation. Never ask to pick.
    $preferred = @('My Orbus Renewed', 'Modded Orbus')
    $candidates = @(Get-ChildItem -LiteralPath $library -Directory | Sort-Object Name)
    $ordered = @()
    foreach ($name in $preferred) {
        $ordered += @($candidates | Where-Object { $_.Name -eq $name })
    }
    $ordered += @($candidates | Where-Object { $_.Name -notin $preferred })
    foreach ($candidate in $ordered) {
        if (Test-ReferenceGame $candidate.FullName) {
            $GameDir = $candidate.FullName
            Write-Host "Automatically found build references: $GameDir" -ForegroundColor Cyan
            break
        }
    }
    if ([string]::IsNullOrWhiteSpace($GameDir)) {
        throw "No compatible generated interop found. Run a BepInEx-prepared RM instance once before building."
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

# Remember selected build references for future releases. No game files change.
[IO.File]::WriteAllText($cachePath, $GameDir, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Build completed. No game instance was changed." -ForegroundColor Green
Write-Host "To publish for RM installs and automatic mod updates, run .\release.ps1." -ForegroundColor Cyan
