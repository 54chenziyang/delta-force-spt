$ErrorActionPreference = 'Stop'

$csc    = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$refDir = 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8'
$src    = 'c:/Users/31902/AppData/Local/Temp/haavk_work/installer/Installer_v12.cs'
$outDir = 'c:/Users/31902/Desktop/temp_20261015_01/DeltaForce_ModPack_v1.2'

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'Delta Force Full Pack v1.2.exe'
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Force }

$refNames = @(
  'mscorlib.dll',
  'System.dll',
  'System.Core.dll',
  'System.Windows.Forms.dll',
  'System.Drawing.dll',
  'System.IO.Compression.dll',
  'System.IO.Compression.FileSystem.dll'
)

$cscArgs = @('/nologo', '/target:winexe', '/langversion:7.3', '/optimize+', '/codepage:65001', '/nostdlib+')
$cscArgs += ('/out:' + $out)
foreach ($name in $refNames) {
  $path = Join-Path $refDir $name
  if (-not (Test-Path -LiteralPath $path)) { throw "Missing reference: $path" }
  $cscArgs += ('/reference:' + $path)
}
$cscArgs += $src

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED ($LASTEXITCODE)"; exit $LASTEXITCODE }
Write-Host ('BUILD OK -> ' + $out + '  ' + (Get-Item -LiteralPath $out).Length + ' bytes')
