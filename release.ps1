# One-command RebornMenu -> GitHub Releases -> RebornManager publisher.
# This script NEVER installs a DLL into a game folder. build.ps1 only
# borrows automatically detected BepInEx interop references for compilation.
#
# .\release.ps1            -> stable DLL offered by RebornManager
# .\release.ps1 -Preview   -> GitHub prerelease (not offered in RM)
# .\release.ps1 -GameDir "..." -> override build REFERENCE folder only

[CmdletBinding()]
param(
    [string] $GameDir = "",
    [switch] $Preview
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repo = 'Hor1zonVR/OrbusRebornMenu'
$root = $PSScriptRoot
$project = Join-Path $root 'src\OrbusRebornMenu\OrbusRebornMenu.csproj'
$pluginFile = Join-Path $root 'src\OrbusRebornMenu\Plugin.cs'
$dll = Join-Path $root 'src\OrbusRebornMenu\bin\Release\net6.0\OrbusRebornMenu.dll'

function Assert-Exit([string] $step) {
    if ($LASTEXITCODE -ne 0) {
        throw "$step failed (exit $LASTEXITCODE). No GitHub release was created by this step."
    }
}
try {
    Set-Location $root
    foreach ($command in @('git', 'gh', 'dotnet')) {
        if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
            throw "Missing $command. Install Git, GitHub CLI and .NET 8 SDK."
        }
    }
    gh auth status
    Assert-Exit "GitHub login (use gh auth login)"

    $branch = (& git branch --show-current).Trim()
    Assert-Exit 'Reading branch'
    if ($branch -ne 'main') { throw 'Switch to main first: git switch main' }
    $remote = (& git remote get-url origin).Trim()
    Assert-Exit 'Reading Git remote'
    if ($remote -notmatch '[/\\:]Hor1zonVR/OrbusRebornMenu(\.git)?$') {
        throw 'Refusing to publish from an unrelated repository.'
    }
    $dirty = @(& git status --porcelain)
    Assert-Exit 'Checking local edits'
    if ($dirty.Count -gt 0) {
        throw 'Commit or stash local source edits first. Refusing to publish unknown or accidental files.'
    }
    & git pull --ff-only origin main
    Assert-Exit 'Syncing newest RebornMenu source'

    $source = Get-Content -LiteralPath $pluginFile -Raw
    $m = [regex]::Match($source,
        '(?s)\[BepInPlugin\(\s*"[^"]+"\s*,\s*"[^"]+"\s*,\s*"(?<v>\d+\.\d+\.\d+)"')
    if (-not $m.Success) { throw 'Plugin.cs is missing a valid BepInPlugin version.' }
    $version = $m.Groups['v'].Value
    $tag = "v$version"

    & git fetch origin --tags
    Assert-Exit 'Fetching release tags'
    $remoteTags = @(& git ls-remote --tags --refs origin 'refs/tags/v*')
    Assert-Exit 'Checking existing Git tags'
    $tags = @{}
    foreach ($line in $remoteTags) {
        if ($line -match 'refs/tags/(v\d+\.\d+\.\d+)$') { $tags[$Matches[1]] = $true }
    }
    if ($tags.ContainsKey($tag)) {
        # We only auto-increment if actual plugin/project source changed
        # since the published version. Documentation-only commits aren't new mods.
        $changed = @(& git diff --name-only $tag HEAD -- 'src/OrbusRebornMenu')
        Assert-Exit 'Checking plugin source changes'
        if ($changed.Count -eq 0) {
            throw "$tag already exists with identical mod source. Make a code change before publishing another version."
        }

        $v = $version.Split('.')
        $patch = [int]$v[2]
        do { $patch++; $version = "$($v[0]).$($v[1]).$patch"; $tag = "v$version" }
        while ($tags.ContainsKey($tag))

        $group = $m.Groups['v']
        $source = $source.Substring(0, $group.Index) + $version +
                  $source.Substring($group.Index + $group.Length)
        [IO.File]::WriteAllText($pluginFile, $source,
            (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "Auto-bumped RebornMenu to $tag." -ForegroundColor Yellow
    }

    Write-Host "Compiling DLL for $tag (NO installation into a game)..." -ForegroundColor Cyan
    & (Join-Path $root 'build.ps1') -GameDir $GameDir
    if ($LASTEXITCODE -ne 0) { throw 'RebornMenu build failed. Not publishing.' }
    if (-not (Test-Path $dll -PathType Leaf)) {
        throw "Compiled DLL missing: $dll"
    }

    # Source changes caused by the automatic version bump are intentional and
    # restricted to Plugin.cs. All actual plugin code was already on main.
    $pending = @(& git status --porcelain)
    Assert-Exit 'Checking staged release files'
    $unexpected = @($pending | Where-Object {
        $_ -notmatch '^ M src/OrbusRebornMenu/Plugin.cs$'
    })
    if ($unexpected.Count -gt 0) {
        throw ('Unexpected working-tree files after build: ' + ($unexpected -join ', '))
    }
    if ($pending.Count -gt 0) {
        & git add -- src/OrbusRebornMenu/Plugin.cs
        Assert-Exit 'Staging version bump'
        & git commit -m "release: RebornMenu $tag"
        Assert-Exit 'Committing version'
        & git push origin main
        Assert-Exit 'Pushing version to GitHub'
    }
    $sha = (& git rev-parse HEAD).Trim()
    Assert-Exit 'Resolving release commit'
    $notes = "Experimental RebornMenu VR prototype. Opens a world-space parchment by Ctrl+Alt+M; left-wrist rune and controller input require headset testing. No generic config editor yet. This is a standalone optional BepInEx plugin, NOT an automatic RM companion."
    $args = @('release','create',$tag,$dll,'--repo',$repo,'--target',$sha,
              '--title',"RebornMenu $tag",'--notes',$notes)
    if ($Preview) { $args += '--prerelease' }
    Write-Host "Publishing $tag to GitHub..." -ForegroundColor Cyan
    & gh @args
    Assert-Exit 'GitHub release upload'
    $result = (& gh release view $tag --repo $repo --json tagName,isPrerelease,assets,url) |
        ConvertFrom-Json
    Assert-Exit 'Verifying public release'
    if ($result.tagName -ne $tag -or
        @($result.assets | Where-Object { $_.name -eq 'OrbusRebornMenu.dll' }).Count -ne 1) {
        throw 'Release published, but did not have exactly one OrbusRebornMenu.dll.'
    }
    Write-Host "SUCCESS: $($result.url)" -ForegroundColor Green
    if ($Preview) {
        Write-Host "Preview release: not shown in RM. Do not promote before headset testing." -ForegroundColor Yellow
    } else {
        Write-Host 'Stable experimental release: in RM, refresh Client Mods -> RebornMenu to install/update.' -ForegroundColor Green
    }
} catch {
    Write-Host "RELEASE STOPPED: $($_.Exception.Message)" -ForegroundColor Red
    throw
}
