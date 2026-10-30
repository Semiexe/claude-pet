param([string]$Snapshot, [string]$SnapState, [string]$MenuShot)
# Starter fuer das Claude Pet. Der Code liegt in src\*.cs und wird beim Start kompiliert.

$here = Split-Path -Parent $PSCommandPath
$files = @(Get-ChildItem (Join-Path $here 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })

Add-Type -Path $files -ReferencedAssemblies System.Windows.Forms, System.Drawing, Microsoft.VisualBasic, System.Web.Extensions

if ($MenuShot) { [PetForm]::MenuSnapshot($MenuShot) }
elseif ($Snapshot) { [PetForm]::Snapshot($Snapshot, $SnapState) }
else { [PetForm]::ScriptPath = $PSCommandPath; [PetForm]::Run() }
