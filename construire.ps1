# Reconstruit l'atlas (si Python est disponible) puis compile MascotteClaude.exe.
# Usage : clic droit > Executer avec PowerShell, ou   powershell -File construire.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (Get-Command python -ErrorAction SilentlyContinue) {
    python outils\construire_atlas.py
    if ($LASTEXITCODE -ne 0) { throw "La construction de l'atlas a echoue." }
}

$net = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
& "$net\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 `
    /out:MascotteClaude.exe /win32icon:assets\icone.ico `
    /resource:assets\claude-atlas.png,atlas.png `
    /r:"$net\WPF\PresentationFramework.dll" /r:"$net\WPF\PresentationCore.dll" /r:"$net\WPF\WindowsBase.dll" /r:System.Xaml.dll `
    src\MascotteClaude.cs
if ($LASTEXITCODE -ne 0) { throw "La compilation a echoue." }
Write-Host "OK : MascotteClaude.exe"
