$managed = 'D:/escape/EscapeFromTarkov/EscapeFromTarkov_Data/Managed'
$core    = 'D:/escape/EscapeFromTarkov/BepInEx/core'
$csc     = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/Roslyn/csc.exe'
$src     = 'c:/Users/31902/AppData/Local/Temp/haavk_work/build/HideoutPrice/src'
$outDir  = 'c:/Users/31902/AppData/Local/Temp/haavk_work/build/HideoutPrice/out'

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = "$outDir/HideoutPrice.dll"
if (Test-Path $out) { Remove-Item $out -Force }

$refs = @(
  "$managed/mscorlib.dll",
  "$managed/netstandard.dll",
  "$managed/System.dll",
  "$managed/System.Core.dll",
  "$managed/Assembly-CSharp.dll",
  "$managed/Comfort.dll",
  "$managed/Unity.TextMeshPro.dll",
  "$managed/UnityEngine.dll",
  "$managed/UnityEngine.CoreModule.dll",
  "$managed/UnityEngine.UI.dll",
  "$managed/UnityEngine.TextRenderingModule.dll",
  "$managed/UnityEngine.TextCoreTextEngineModule.dll",
  "$managed/UnityEngine.TextCoreFontEngineModule.dll",
  "$managed/Sirenix.Serialization.dll",
  "$managed/Sirenix.Utilities.dll",
  "$core/BepInEx.dll",
  "$core/0Harmony.dll"
)

$cscArgs = @('/nologo','/target:library','/langversion:latest','/optimize+')
$cscArgs += ('/out:' + $out)
foreach ($r in $refs) { $cscArgs += ('/reference:' + $r) }
$cscArgs += (Get-ChildItem $src -Filter *.cs | ForEach-Object { $_.FullName })

& $csc $cscArgs
if ($LASTEXITCODE -eq 0) { Write-Host "BUILD OK -> $out" } else { Write-Host "BUILD FAILED" }
