param([switch]$Remove, [switch]$NoToolDetails)
# -NoToolDetails: ohne den Hook vor jedem Werkzeug-Aufruf (spart Rechenzeit, dann ohne Symbole fuer "liest", "schreibt" ...)
# Traegt die Claude-Pet-Hooks in ~/.claude/settings.json ein (oder mit -Remove wieder aus).
# Sicherungen: settings.json.bak-claudepet (der Stand vor dem ersten Lauf, bleibt) und
# settings.json.bak-claudepet-last (der Stand vor dem letzten Lauf).
$ErrorActionPreference = 'Stop'
$settings = Join-Path $env:USERPROFILE '.claude\settings.json'
$hook = Join-Path $PSScriptRoot 'ClaudePetHook.ps1'
# Ereignis -> Art der Meldung; async = laeuft im Hintergrund und bremst Claude Code nicht aus
$events = [ordered]@{
    UserPromptSubmit  = @{ kind = 'running'; async = $true }
    PermissionRequest = @{ kind = 'needs';   async = $true }
    PreToolUse        = @{ kind = 'pretool'; async = $true }
    Notification      = @{ kind = 'notify';  async = $true }
    PostToolUse       = @{ kind = 'tool';    async = $true }
    Stop              = @{ kind = 'ready';   async = $true }
    StopFailure       = @{ kind = 'blocked'; async = $true }
    SubagentStart     = @{ kind = 'agentstart';   async = $true }
    SubagentStop      = @{ kind = 'agentstop';    async = $true }
    PreCompact        = @{ kind = 'compact';      async = $true }
    PostCompact       = @{ kind = 'compactdone';  async = $true }
    SessionEnd        = @{ kind = 'end';     async = $false }
}

if (Test-Path $settings) {
    try { $obj = Get-Content $settings -Raw | ConvertFrom-Json }
    catch { Write-Host "settings.json laesst sich nicht lesen ($($_.Exception.Message)). Es wurde nichts geaendert."; exit 1 }
    if ($null -eq $obj) { $obj = [pscustomobject]@{} }
    $backup = $settings + '.bak-claudepet'
    if (-not (Test-Path $backup)) { Copy-Item $settings $backup }
    Copy-Item $settings ($settings + '.bak-claudepet-last') -Force
} else {
    New-Item -ItemType Directory -Force (Split-Path $settings) | Out-Null
    $obj = [pscustomobject]@{}
}
if (-not $obj.PSObject.Properties['hooks']) {
    $obj | Add-Member -NotePropertyName hooks -NotePropertyValue ([pscustomobject]@{})
}

function Test-Ours($h) { return ([string]$h.command) -match 'ClaudePetHook' }

foreach ($ev in $events.Keys) {
    $existing = @()
    if ($obj.hooks.PSObject.Properties[$ev]) { $existing = @($obj.hooks.$ev) }
    # Nur die eigenen frueheren Hooks entfernen. Ein Eintrag mit fremden Hooks daneben bleibt mit diesen erhalten.
    $keep = @()
    foreach ($grp in $existing) {
        $inner = @()
        if ($grp -and $grp.PSObject.Properties['hooks'] -and $grp.hooks) { $inner = @($grp.hooks) }
        $others = @($inner | Where-Object { -not (Test-Ours $_) })
        if ($inner.Count -eq 0 -or $others.Count -eq $inner.Count) { $keep += $grp }       # nichts von uns darin
        elseif ($others.Count -gt 0) { $grp.hooks = $others; $keep += $grp }              # nur unsere Hooks herausnehmen
        # sonst bestand der Eintrag nur aus unseren Hooks: er faellt weg
    }
    if (-not $Remove -and -not ($ev -eq 'PreToolUse' -and $NoToolDetails)) {
        $cmd = 'powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $hook + '" ' + $events[$ev].kind
        $h = [ordered]@{ type = 'command'; command = $cmd }
        if ($events[$ev].async) { $h['async'] = $true }
        $keep += [pscustomobject]@{ hooks = @([pscustomobject]$h) }
    }
    if ($obj.hooks.PSObject.Properties[$ev]) { $obj.hooks.PSObject.Properties.Remove($ev) }
    if ($keep.Count -gt 0) { $obj.hooks | Add-Member -NotePropertyName $ev -NotePropertyValue $keep }
}
if (@($obj.hooks.PSObject.Properties).Count -eq 0) { $obj.PSObject.Properties.Remove('hooks') }

# Mit voller Tiefe schreiben. Wuerde etwas abgeschnitten, bricht das Skript ab, statt die Datei zu beschaedigen.
$json = $obj | ConvertTo-Json -Depth 100 -WarningVariable jsonWarn -WarningAction SilentlyContinue
if ($jsonWarn) { Write-Host "Abbruch: settings.json waere beim Speichern verkuerzt worden ($jsonWarn). Es wurde nichts geaendert."; exit 1 }
try { $null = $json | ConvertFrom-Json } catch { Write-Host "Abbruch: Das Ergebnis ist kein gueltiges JSON. Es wurde nichts geaendert."; exit 1 }

# erst in eine Hilfsdatei schreiben und dann austauschen, damit nie eine halbe Datei liegt
$tmp = $settings + '.tmp-claudepet'
[IO.File]::WriteAllText($tmp, $json, (New-Object Text.UTF8Encoding($false)))
if (Test-Path $settings) { [IO.File]::Replace($tmp, $settings, [NullString]::Value) } else { Move-Item $tmp $settings }
if ($Remove) { 'Claude-Pet-Hooks entfernt.' } else { 'Claude-Pet-Hooks eingetragen. Neue Claude-Code-Sitzungen melden ihren Status ans Pet.' }
