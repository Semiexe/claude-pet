param([string]$Kind)
# Wird von Claude Code bei Ereignissen aufgerufen und schreibt den Zustand der Sitzung,
# den das Pet dann anzeigt.
# Kind: running | pretool | tool | ready | needs | notify | blocked | agentstart | agentstop | compact | compactdone | end

function Find-AncestorWindow {
    # Sucht den naechsten Vorfahren-Prozess mit Hauptfenster (Terminal, Claude-App, Editor)
    try {
        $map = @{}
        Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId |
            ForEach-Object { $map[[int]$_.ProcessId] = [int]$_.ParentProcessId }
        $p = $PID
        for ($n = 0; $n -lt 15; $n++) {
            if (-not $map.ContainsKey($p)) { break }
            $p = $map[$p]
            $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
            if ($proc -and $proc.MainWindowHandle -ne [IntPtr]::Zero) { return [string]$proc.MainWindowHandle.ToInt64() }
        }
    } catch { }
    return '0'
}

function Get-ToolInfo($j) {
    # Kategorie und eine kurze, datensparsame Angabe zum Werkzeug (nur Dateiname, erstes Wort, Host)
    if (-not $j -or -not $j.tool_name) { return @('', '') }
    $name = [string]$j.tool_name
    $in = $j.tool_input
    $cat = 'other'
    switch -Regex ($name) {
        '^(Read|Glob|Grep|LS|NotebookRead)$'                   { $cat = 'read' }
        '^(Edit|Write|MultiEdit|NotebookEdit)$'                { $cat = 'write' }
        '^(WebSearch|WebFetch)$'                               { $cat = 'search' }
        '^(Bash|PowerShell|BashOutput|KillShell|KillBash)$'    { $cat = 'exec' }
        '^(Task|Agent)$'                                       { $cat = 'agent' }
        default                                                { $cat = 'other' }
    }
    $detail = ''
    try {
        if ($in) {
            if ($in.file_path)          { $detail = Split-Path ([string]$in.file_path) -Leaf }
            elseif ($in.notebook_path)  { $detail = Split-Path ([string]$in.notebook_path) -Leaf }
            elseif ($in.command)        { $detail = ([string]$in.command).Trim().Split(' ', 2)[0] }
            elseif ($name -eq 'WebFetch' -and $in.url) { $detail = ([uri][string]$in.url).Host }
        }
    } catch { }
    $detail = ($detail -replace '[\r\n\t|]+', ' ').Trim()
    if ($detail.Length -gt 40) { $detail = $detail.Substring(0, 40) }
    return @($cat, $detail)
}

function Get-Preview($j) {
    # Erste Zeile von Claudes Antwort, nur wenn der Schalter "Antwort-Vorschau" im Pet an ist (Datei preview.on)
    try {
        if (-not (Test-Path (Join-Path $base 'preview.on'))) { return '' }
        if (-not $j -or -not $j.last_assistant_message) { return '' }
        foreach ($l in (([string]$j.last_assistant_message) -split "`n")) {
            $t = ($l -replace '[#*`>_\[\]]', '').Trim()
            if ($t.Length -gt 0) { if ($t.Length -gt 90) { $t = $t.Substring(0, 90).TrimEnd() + '...' }; return $t }
        }
    } catch { }
    return ''
}

try {
    $raw = [Console]::In.ReadToEnd()
    $j = $null
    if ($raw) { $j = $raw | ConvertFrom-Json }
    $sid = 'unknown'
    if ($j -and $j.session_id) { $sid = ([string]$j.session_id) -replace '[^\w\-]', '_' }
    $base = $env:CLAUDEPET_HOME
    if (-not $base) { $base = Join-Path $env:APPDATA 'ClaudePet' }
    $dir = Join-Path $base 'status'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $file = Join-Path $dir ($sid + '.txt')

    $old = @{}
    $age = 9999.0
    if (Test-Path $file) {
        $age = ((Get-Date) - (Get-Item $file).LastWriteTime).TotalSeconds
        foreach ($l in (Get-Content $file -Encoding UTF8)) {
            $i = $l.IndexOf('=')
            if ($i -gt 0) { $old[$l.Substring(0, $i)] = $l.Substring($i + 1) }
        }
    }
    # die letzten Schritte, neuester zuerst (h1 .. h5)
    $hist = @()
    foreach ($n in 1..5) { if ($old["h$n"]) { $hist += $old["h$n"] } }

    $tool = ''; $detail = ''; $msg = ''; $err = ''
    $title = $old['title']
    $agents = 0; if ($old['agents']) { [int]::TryParse($old['agents'], [ref]$agents) | Out-Null }
    $compact = $old['compact']
    if ($j -and $j.session_title) { $title = (([string]$j.session_title) -replace '[\r\n\t|]+', ' ').Trim(); if ($title.Length -gt 60) { $title = $title.Substring(0, 60) } }

    switch ($Kind) {
        'end'     { Remove-Item $file -ErrorAction SilentlyContinue; return }
        # nach einem Werkzeug-Aufruf: wartete die Sitzung auf eine Erlaubnis, laeuft sie jetzt weiter
        'tool'    {
            if ($old['state'] -ne 'needs' -and $old['state'] -ne 'blocked') { return }
            $state = 'running'; $tool = $old['tool']; $detail = $old['detail']
        }
        # vor einem Werkzeug-Aufruf: Kategorie merken. Eine gerade gemeldete Frage oder ein Ende bleibt bestehen
        'pretool' {
            $tool, $detail = Get-ToolInfo $j
            if ($old['state'] -eq 'ready' -and $age -lt 2) { return }
            if ($old['state'] -eq 'needs' -and $age -lt 8) { $state = 'needs' } else { $state = 'running' }
            $entry = "$tool|$detail"
            if ($tool -and ($hist.Count -eq 0 -or $hist[0] -ne $entry)) { $hist = @($entry) + $hist }
            # Tageszaehler: nur die Art des Werkzeugs, keine Namen
            if ($tool) { try { [IO.File]::AppendAllText((Join-Path $base 'tools.log'), (Get-Date).ToString('yyyyMMdd') + '|' + $tool + "`r`n") } catch { } }
        }
        'running' { $state = 'running'; $hist = @(); $agents = 0; $compact = '' }
        'ready'   { $state = 'ready'; $msg = Get-Preview $j; $agents = 0; $compact = '' }
        'needs'   { $state = 'needs'; $tool, $detail = Get-ToolInfo $j }
        'blocked' { $state = 'blocked'; $agents = 0; $compact = ''; if ($j -and $j.error) { $err = (([string]$j.error) -replace '[^\w\-]', '') } }
        # Helfer (Subagenten) zaehlen, Zustand bleibt
        'agentstart' { $state = $old['state']; if (-not $state) { $state = 'running' }; $agents = [Math]::Min(9, $agents + 1); $tool = $old['tool']; $detail = $old['detail'] }
        'agentstop'  { $state = $old['state']; if (-not $state) { return }; $agents = [Math]::Max(0, $agents - 1); $tool = $old['tool']; $detail = $old['detail'] }
        # Claude fasst den Kontext zusammen
        'compact'     { $state = $old['state']; if (-not $state) { $state = 'running' }; $compact = '1'; $tool = $old['tool']; $detail = $old['detail'] }
        'compactdone' { $state = $old['state']; if (-not $state) { return }; $compact = ''; $tool = $old['tool']; $detail = $old['detail'] }
        'notify'  {
            $type = ''
            if ($j -and $j.notification_type) { $type = [string]$j.notification_type }
            if ($type -eq 'idle_prompt') { $state = 'ready' }
            elseif ($type -eq 'permission_prompt' -or $type -eq 'elicitation_dialog') { $state = 'needs'; $tool = $old['tool']; $detail = $old['detail'] }
            else { return }
        }
        default   { return }
    }
    if ($hist.Count -gt 5) { $hist = $hist[0..4] }

    $proj = $old['project']
    if ($j -and $j.cwd) { $proj = Split-Path ([string]$j.cwd) -Leaf }
    $hwnd = $old['hwnd']
    if (-not $hwnd -or $hwnd -eq '0') { $hwnd = Find-AncestorWindow }

    $lines = @("state=$state", "project=$proj", "hwnd=$hwnd", "tool=$tool", "detail=$detail")
    if ($title) { $lines += "title=$title" }
    if ($agents -gt 0) { $lines += "agents=$agents" }
    if ($compact -eq '1') { $lines += 'compact=1' }
    if ($err) { $lines += "err=$err" }
    for ($n = 0; $n -lt $hist.Count; $n++) { $lines += ('h' + ($n + 1) + '=' + $hist[$n]) }
    if ($msg) { $lines += "msg=$msg" }
    Set-Content -Path $file -Value $lines -Encoding UTF8
} catch { }
