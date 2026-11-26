# Makes a self-contained build in .\publish and the setup file in .\installer\Output
# Needs: .NET 11 SDK, Inno Setup 6 (https://jrsoftware.org/isdl.php)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$publish = Join-Path $root 'publish'

if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }

Write-Host 'Publishing self-contained build...'
dotnet publish "$root\Drawski.csproj" -c Release -r win-x64 --self-contained true -p:DebugType=none -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php or run: winget install JRSoftware.InnoSetup' }

Write-Host 'Building installer...'
& $iscc /Q "$root\installer\Drawski.iss"
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed' }

Get-ChildItem "$root\installer\Output\*.exe" | ForEach-Object { Write-Host ("Done: {0} ({1:0.0} MB)" -f $_.FullName, ($_.Length / 1MB)) }
