# Baut aus src\*.cs die Datei ClaudePet.exe (mit Symbol). Danach startet "Claude Pet starten.bat" die .exe
# statt des Skripts: schneller, ohne verstecktes PowerShell-Fenster, mit Wächter, der das Pet nach einem Absturz neu startet.
# Wer das Pet ändert, ruft dieses Skript einfach erneut auf.
$here = Split-Path -Parent $PSCommandPath
$out = Join-Path $here 'ClaudePet.exe'
$ico = Join-Path $here 'ClaudePet.ico'
$files = @(Get-ChildItem (Join-Path $here 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })

# Laufendes Pet beenden: erst den Wächter (ohne --run), sonst startet er das Pet sofort neu
$procs = @(Get-CimInstance Win32_Process -Filter "Name='ClaudePet.exe'")
foreach ($p in ($procs | Where-Object { $_.CommandLine -notmatch '--run' })) { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue }
foreach ($p in ($procs | Where-Object { $_.CommandLine -match '--run' })) { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Milliseconds 500

# Symbol aus dem Pixel-Pet zeichnen (24 x 16 Zellen, je 2 Pixel)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 48, 48, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Transparent)
$body = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(219, 119, 98))
$shade = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(196, 102, 84))
$eye = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(42, 23, 16))
function Cell($b, $c, $r, $w) { $g.FillRectangle($b, $c * 2, 8 + $r * 2, $w * 2, 2) }
foreach ($r in 0..11) { Cell $body 4 $r 16 }
Cell $shade 4 11 16
foreach ($r in 5..8) { Cell $body 0 $r 4; Cell $body 20 $r 4 }
Cell $shade 0 8 4; Cell $shade 20 8 4
foreach ($r in 12..15) { foreach ($c in 6, 10, 14, 18) { Cell $body $c $r 2 } }
foreach ($e in 5, 15) { Cell $eye ($e + 1) 2 2; Cell $eye $e 3 1; Cell $eye ($e + 3) 3 1 }
$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$fs = [IO.File]::Create($ico); $icon.Save($fs); $fs.Close()
$g.Dispose(); $bmp.Dispose()

# Kompilieren
$prov = New-Object Microsoft.CSharp.CSharpCodeProvider
$cp = New-Object System.CodeDom.Compiler.CompilerParameters
$cp.GenerateExecutable = $true
$cp.OutputAssembly = $out
[void]$cp.ReferencedAssemblies.AddRange(@('System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', 'Microsoft.VisualBasic.dll', 'System.Web.Extensions.dll'))
$cp.CompilerOptions = '/target:winexe /optimize+ /win32icon:"' + $ico + '"'
try { $res = $prov.CompileAssemblyFromFile($cp, [string[]]$files) }
catch { "Das Bauen ist fehlgeschlagen: " + $_.Exception.Message; exit 1 }
if ($res.Errors.HasErrors) {
    "Fehler beim Bauen:"
    foreach ($e in $res.Errors) { if (-not $e.IsWarning) { "  " + $e.FileName + " Zeile " + $e.Line + ": " + $e.ErrorText } }
    exit 1
}
if (-not (Test-Path $out)) { "Das Bauen hat keine Datei erzeugt."; exit 1 }
"Fertig: $out ($([math]::Round((Get-Item $out).Length / 1KB)) KB)"
"Starten mit 'Claude Pet starten.bat'. Beenden mit 'Claude Pet beenden.bat' oder im Menue des Pets."
