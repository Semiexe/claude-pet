param([switch]$Remove, [switch]$NoToolDetails)
# -NoToolDetails: ohne den Hook vor jedem Werkzeug-Aufruf (spart Rechenzeit, dann ohne Symbole fuer "liest", "schreibt" ...)
# Traegt die Claude-Pet-Hooks in ~/.claude/settings.json ein (oder mit -Remove wieder aus).
# Vorher wird einmalig eine Sicherung settings.json.bak-claudepet angelegt.
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
    $backup = $settings + '.bak-claudepet'
    if (-not (Test-Path $backup)) { Copy-Item $settings $backup }
    $obj = Get-Content $settings -Raw | ConvertFrom-Json
} else {
    New-Item -ItemType Directory -Force (Split-Path $settings) | Out-Null
    $obj = [pscustomobject]@{}
}
if (-not $obj.PSObject.Properties['hooks']) {
    $obj | Add-Member -NotePropertyName hooks -NotePropertyValue ([pscustomobject]@{})
}

foreach ($ev in $events.Keys) {
    $existing = @()
    if ($obj.hooks.PSObject.Properties[$ev]) { $existing = @($obj.hooks.$ev) }
    # eigene fruehere Eintraege entfernen, fremde Hooks bleiben unberuehrt
    $keep = @($existing | Where-Object { -not ((@($_.hooks) | ForEach-Object { [string]$_.command }) -match 'ClaudePetHook') })
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

$json = $obj | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($settings, $json, (New-Object Text.UTF8Encoding($false)))
if ($Remove) { 'Claude-Pet-Hooks entfernt.' } else { 'Claude-Pet-Hooks eingetragen. Neue Claude-Code-Sitzungen melden ihren Status ans Pet.' }
