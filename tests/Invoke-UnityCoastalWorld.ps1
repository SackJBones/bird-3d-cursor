param(
 [Parameter(Mandatory=$true)][string]$UnityEditor,
 [Parameter(Mandatory=$true)][string]$ProjectPath,
 [ValidateSet('Android','Windows','Both')][string]$Platform='Android',
 [switch]$Create,[switch]$ReviseR05,[switch]$RefineR05,[switch]$RepairR06,[switch]$RefineRockR06,[switch]$AddBird,[switch]$AddSocial,[switch]$AddBeacons,[switch]$AddPractice,[switch]$PrepareLighting,[switch]$RefineLighting,[switch]$SmoothLightingJoins,[switch]$AddVista,[switch]$RefineVista,[switch]$UpdateVistaMeshes,[switch]$CheckVista,[switch]$AddPond,[switch]$UpdatePondMeshes,[switch]$CheckPond,[switch]$AddFish,[switch]$CheckFish,[switch]$BakeLighting,[switch]$CheckLighting,[switch]$CheckBird,[switch]$CheckSocial,[switch]$CheckBeacons,[switch]$Check,[switch]$Walk,[switch]$Build,[switch]$BuildVistaInspection
)
$ErrorActionPreference='Stop'
if($BuildVistaInspection -and $Platform -ne 'Android'){throw '-BuildVistaInspection requires -Platform Android.'}
if($ReviseR05 -and $RefineR05){throw '-ReviseR05 already includes the refinement; use -RefineR05 only for the first-pass R05 assets.'}
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
$repo=Split-Path $PSScriptRoot -Parent
$editor=Join-Path $project 'Assets/BirdGenerated/Editor'
New-Item -ItemType Directory -Force $editor | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat/Editor') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $editor $_.Name) }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityCoastalWorldChecks.cs') -Destination $editor
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityCoastalRepairChecks.cs') -Destination $editor
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityPondChecks.cs') -Destination $editor
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityWorldBundleChecks.cs') -Destination $editor
$runtime=Join-Path $project 'Assets/BirdGenerated/Runtime'
New-Item -ItemType Directory -Force $runtime | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityCoastalWalkChecks.cs') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityPersonalBirdChecks.cs') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnitySocialBirdChecks.cs') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityBeaconChecks.cs') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityLightingChecks.cs') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityVistaChecks.cs') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityPondFishChecks.cs') -Destination $runtime
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat') -File | Where-Object { $_.Name -match '\.cs(\.meta)?$' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $runtime $_.Name) }
$presentation=Join-Path $project 'Assets/BirdGenerated/Presentation'
New-Item -ItemType Directory -Force $presentation | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat/Presentation') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $presentation $_.Name) }
foreach($name in @('BirdLogicalDepth.shader','BirdLogicalDepth.shader.meta')) { Copy-Item -LiteralPath (Join-Path $repo ('Unity/BirdPlugin/Runtime/Presentation/'+$name)) -Destination $presentation }
function Invoke-Coastal([string]$Method,[string]$Stem,[string]$Target) {
 $result=Join-Path $project ($Stem+'-result.txt');$log=Join-Path $project ($Stem+'.log')
 Set-Content -LiteralPath $result -Value 'PENDING'
 $process=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-buildTarget',$Target,'-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
 $null=$process.Handle
 if(!$process.WaitForExit(600000)){$process.Kill();throw "Unity coastal step timed out: $log"}
 $exitCode=$process.ExitCode
 $summary=Get-Content -Raw -LiteralPath $result;Write-Output $summary
 Copy-Item -LiteralPath $result -Destination (Join-Path $project ($Stem+'-'+$Target+'-result.txt'))
 Copy-Item -LiteralPath $log -Destination (Join-Path $project ($Stem+'-'+$Target+'.log'))
 Set-Content -LiteralPath (Join-Path $project ($Stem+'-'+$Target+'-exit.txt')) -Value ([string]$exitCode)
 if($exitCode -ne 0 -or !$summary.StartsWith('PASS:')){throw "Unity coastal step failed (exit code '$exitCode'): $log"}
}
$targets=if($Platform -eq 'Both'){@('StandaloneWindows64','Android')}elseif($Platform -eq 'Windows'){@('StandaloneWindows64')}else{@('Android')}
foreach($target in $targets){
 $evidence=Join-Path (Split-Path $project -Parent) 'Validation/CoastalWorld'
 $targetEvidence=Join-Path $evidence $target
 New-Item -ItemType Directory -Force $targetEvidence | Out-Null
 Invoke-Coastal 'UnityWorldSdkSetup.Run' 'world-sdk-setup' $target
 if($Create){Invoke-Coastal 'UnityCoastalWorldChecks.Author' 'coastal-author' $target;$Create=$false}
 if($ReviseR05){Invoke-Coastal 'UnityCoastalWorldChecks.ReviseR05' 'coastal-r05' $target;$ReviseR05=$false}
 if($RefineR05){Invoke-Coastal 'UnityCoastalWorldChecks.RefineR05' 'coastal-r05-refine' $target;$RefineR05=$false}
 if($RepairR06){Invoke-Coastal 'UnityCoastalWorldChecks.RepairR06' 'coastal-r06' $target;$RepairR06=$false}
 if($RefineRockR06){Invoke-Coastal 'UnityCoastalWorldChecks.RefineRockR06' 'coastal-r06-rock' $target;$RefineRockR06=$false}
 if($AddBird){Invoke-Coastal 'UnityCoastalWorldChecks.AddBird' 'coastal-bird-author' $target;$AddBird=$false}
 if($AddSocial){Invoke-Coastal 'UnityCoastalWorldChecks.AddSocial' 'coastal-social-author' $target;$AddSocial=$false}
 if($AddBeacons){Invoke-Coastal 'UnityCoastalWorldChecks.AddBeacons' 'coastal-beacons-author' $target;$AddBeacons=$false}
 if($AddPractice){Invoke-Coastal 'UnityCoastalWorldChecks.AddPractice' 'coastal-practice-author' $target;$AddPractice=$false}
 if($PrepareLighting){Invoke-Coastal 'UnityCoastalWorldChecks.PrepareLighting' 'coastal-lighting-prepare' $target;$PrepareLighting=$false}
 if($RefineLighting){Invoke-Coastal 'UnityCoastalWorldChecks.RefineLighting' 'coastal-lighting-refine' $target;$RefineLighting=$false}
 if($SmoothLightingJoins){Invoke-Coastal 'UnityCoastalWorldChecks.SmoothLightingJoins' 'coastal-lighting-joins' $target;$SmoothLightingJoins=$false}
 if($AddVista){Invoke-Coastal 'UnityCoastalWorldChecks.AddVista' 'coastal-vista-author' $target;$AddVista=$false}
 if($RefineVista){Invoke-Coastal 'UnityCoastalWorldChecks.RefineVista' 'coastal-vista-refine' $target;$RefineVista=$false}
 if($AddPond){Invoke-Coastal 'UnityCoastalWorldChecks.AddPond' 'coastal-pond-author' $target;$AddPond=$false}
 if($AddFish){Invoke-Coastal 'UnityCoastalWorldChecks.AddFish' 'coastal-fish-author' $target;$AddFish=$false}
 if($UpdatePondMeshes){Invoke-Coastal 'UnityCoastalWorldChecks.UpdatePondMeshes' 'coastal-pond-meshes' $target;$UpdatePondMeshes=$false}
 if($BakeLighting){Invoke-Coastal 'UnityCoastalWorldChecks.BakeLighting' 'coastal-lighting-bake' $target;$BakeLighting=$false}
 if($UpdateVistaMeshes){Invoke-Coastal 'UnityCoastalWorldChecks.UpdateVistaMeshes' 'coastal-vista-meshes' $target;$UpdateVistaMeshes=$false}
 if($CheckVista){Invoke-Coastal 'UnityVistaChecks.Run' 'coastal-vista-check' $target}
 if($CheckPond){Invoke-Coastal 'UnityCoastalWorldChecks.CheckPond' 'coastal-pond-check' $target}
 if($CheckFish){Invoke-Coastal 'UnityPondFishChecks.Run' 'coastal-fish-check' $target}
 if($CheckLighting){Invoke-Coastal 'UnityLightingChecks.Run' 'coastal-lighting-check' $target}
 if($CheckBird){Invoke-Coastal 'UnityPersonalBirdChecks.Run' 'coastal-bird-check' $target}
 if($CheckSocial){Invoke-Coastal 'UnitySocialBirdChecks.Run' 'coastal-social-check' $target}
 if($CheckBeacons){Invoke-Coastal 'UnityBeaconChecks.Run' 'coastal-beacons-check' $target}
 if($Check){
  Invoke-Coastal 'UnityCoastalWorldChecks.Check' 'coastal-check' $target
  Get-ChildItem -LiteralPath $evidence -File | Where-Object { $_.Extension -eq '.png' -or $_.Name -in @('routes.csv','walk-routes.json','geometry-budget.txt','editability.txt','passage-clearance.txt','r06-before.txt','r06-repair-checks.txt') -or $_.Name -like 'route-*.txt' } | Copy-Item -Destination $targetEvidence
 }
 if($Walk){
  Invoke-Coastal 'UnityCoastalWalkChecks.Run' 'coastal-walk' $target
  Copy-Item -LiteralPath (Join-Path $evidence 'walkthrough.csv') -Destination $targetEvidence
 }
 if($Build){Invoke-Coastal 'UnityCoastalWorldChecks.Build' 'coastal-build' $target}
 if($BuildVistaInspection){Invoke-Coastal 'UnityCoastalWorldChecks.BuildVistaInspection' 'coastal-vista-inspection-build' $target}
}
