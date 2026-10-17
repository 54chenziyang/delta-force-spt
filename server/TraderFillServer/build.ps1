$ErrorActionPreference = 'Stop'

$runtime = 'D:/escape/EscapeFromTarkov/SPT_Runtime'
$net     = 'C:/Program Files/dotnet/shared/Microsoft.NETCore.App/10.0.13'
$csc     = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$src     = 'c:/Users/31902/AppData/Local/Temp/haavk_work/build/TraderFillServer/src'
$outDir  = 'c:/Users/31902/AppData/Local/Temp/haavk_work/build/TraderFillServer/out'

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'Haavk.TraderFill.dll'
if (Test-Path $out) { Remove-Item $out -Force }

$native = @(
  'System.IO.Compression.Native.dll', 'msquic.dll', 'mscorrc.dll', 'mscordbi.dll',
  'mscordaccore.dll', 'hostpolicy.dll', 'coreclr.dll', 'clrjit.dll', 'clrgc.dll',
  'clrgcexp.dll', 'clretwrc.dll'
)
$refs = @()
$refs += (Get-ChildItem $net -Filter *.dll |
  Where-Object { $native -notcontains $_.Name -and $_.Name -notlike 'mscordaccore_*' -and $_.Name -notlike 'Microsoft.DiaSymReader.Native*' } |
  ForEach-Object { $_.FullName })
$refs += @(
  "$runtime/SPTarkov.Server.Core.dll",
  "$runtime/SPTarkov.Common.dll",
  "$runtime/SPTarkov.DI.dll",
  "$runtime/SPTarkov.Reflection.dll",
  "$runtime/SPTarkov.Server.Assets.dll",
  "$runtime/SemanticVersioning.dll",
  "$runtime/0Harmony.dll"
)

$cscArgs = @('/nologo', '/target:library', '/langversion:latest', '/optimize+', '/nostdlib+')
$cscArgs += ('/out:' + $out)
foreach ($r in $refs) { $cscArgs += ('/reference:' + $r) }
$cscArgs += (Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName })

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED ($LASTEXITCODE)"; exit $LASTEXITCODE }
Write-Host ("BUILD OK -> " + $out + "  " + (Get-Item $out).Length + " bytes")
