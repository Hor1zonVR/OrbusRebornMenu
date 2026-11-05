# Run with -Apply to remove ONLY known retired config entries from installed
# Orbus modding instances. The default is preview-only.
# Requires the updated Ability Labels and QuestHelper releases first.
[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$Root = "$env:LOCALAPPDATA\OrbusRebornModdingInstances"
)

$ErrorActionPreference = 'Stop'

if ($Apply -and (Get-Process -Name 'OrbusVR' -ErrorAction SilentlyContinue)) {
    throw "Close OrbusVR before applying config cleanup, so the game cannot write old settings back."
}

# Do not touch active production settings. Old keys that still have a
# running Config.Bind in another mod must NOT be pruned here.
$obsolete = @{
    'com.horizon.orbusabilitylabels.cfg' = @(
        'Bard Inspector|ShowCompactPanel'
        'Bard Inspector|ShowTemporaryPanel'
        'Bard Inspector|PanelScale'
        'Bard Inspector|SideOffsetMetres'
        'Bard Inspector|HeightMetres'
        'Bard Inspector|TextSize'
        'Diagnostics|EnableManualCapture'
        'Diagnostics|NearbyScanRadiusMetres'
        'Labels|TextSize'
    )
    'com.horizon.orbus.questhelper.cfg' = @(
        'Missions|EnableRegionTaskGrouping'
        'Missions|EnableHtmlRegionTaskGrouping'
        'Missions|SpadesMode'
    )
}
if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
    throw "Instances folder not found: $Root"
}

$files = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Filter '*.cfg' |
    Where-Object { $obsolete.ContainsKey($_.Name.ToLowerInvariant()) })

if ($files.Count -eq 0) {
    Write-Host "No matching Ability Labels / QuestHelper configs under: $Root"
    exit 0
}

foreach ($file in $files) {
    $keysToRemove = @{}
    foreach ($pair in $obsolete[$file.Name.ToLowerInvariant()]) {
        $keysToRemove[$pair] = $true
    }

    # Retain all unrelated lines and unknown mod-specific keys. Remove a
    # generated comment block immediately preceding a deleted key.
    $inputLines = [System.IO.File]::ReadAllLines($file.FullName)
    $output = New-Object 'System.Collections.Generic.List[string]'
    $pending = New-Object 'System.Collections.Generic.List[string]'
    $section = ''
    $removed = New-Object 'System.Collections.Generic.List[string]'

    foreach ($line in $inputLines) {
        $trimmed = $line.Trim()
        if ($trimmed -match '^\[([^\]]+)\]$') {
            foreach ($comment in $pending) { $output.Add($comment) }
            $pending.Clear()
            $section = $Matches[1]
            $output.Add($line)
            continue
        }

        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) {
            $pending.Add($line)
            continue
        }

        if ($trimmed -match '^([^=]+?)\s*=') {
            $key = $Matches[1].Trim()
            $pair = "$section|$key"
            if ($keysToRemove.ContainsKey($pair)) {
                $pending.Clear()
                $removed.Add($pair)
                continue
            }
        }

        foreach ($comment in $pending) { $output.Add($comment) }
        $pending.Clear()
        $output.Add($line)
    }
    foreach ($comment in $pending) { $output.Add($comment) }

    if ($removed.Count -eq 0) {
        Write-Host "Already clean: $($file.FullName)"
        continue
    }

    Write-Host ("{0} {1}: {2}" -f $(if ($Apply) { 'CLEAN' } else { 'PREVIEW' }),
        $file.FullName, ($removed -join ', '))

    if (-not $Apply) { continue }

    # Backup preserves all hand-edited values and any original formatting.
    $backup = $file.FullName + '.before-RebornMenu-cleanup.bak'
    if (Test-Path -LiteralPath $backup) {
        # Never overwrite a previous backup.
        $backup = $file.FullName + '.' +
            (Get-Date -Format 'yyyyMMdd-HHmmss') + '.bak'
    }
    Copy-Item -LiteralPath $file.FullName -Destination $backup -ErrorAction Stop

    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText(
        $file.FullName, (($output.ToArray() -join "`r`n") + "`r`n"), $enc)
    Write-Host "Backup saved: $backup"
}
Write-Host 'Completed. The preview makes no edits; -Apply only changes known obsolete keys.'
