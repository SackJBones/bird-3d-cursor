param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [switch]$Generate,
    [switch]$RefineLayout,
    [switch]$Check,
    [switch]$Launch
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
if(!(Test-Path -LiteralPath (Join-Path $project 'Assets/BirdWorld/Scenes/BirdFeasibility.unity'))) { throw 'Use the maintained BirdWorld project.' }
if(!(Test-Path -LiteralPath $UnityEditor)) { throw 'Unity executable missing.' }
$runtime=Join-Path $project 'Assets/BirdGenerated/Runtime'; $editor=Join-Path $project 'Assets/BirdGenerated/Editor'
New-Item -ItemType Directory -Force $runtime,$editor | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat') -File | Where-Object { $_.Name -match '\.cs(\.meta)?$' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $runtime $_.Name) }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityTrackingLabChecks.cs') -Destination $runtime
foreach($helper in @('UnityTrackingLab.cs','UnityWorldBundleChecks.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $helper) -Destination $editor }
function Invoke-LabUnity([string]$Method,[string]$Stem) {
    $result=Join-Path $project ($Stem+'-result.txt'); $log=Join-Path $project ($Stem+'.log')
    Set-Content -LiteralPath $result -Value 'PENDING'
    $process=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-buildTarget','Android','-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    if(!$process.WaitForExit(600000)) { $process.Kill(); throw "Unity lab step timed out: $log" }
    $process.Refresh(); $summary=Get-Content -Raw -LiteralPath $result; Write-Output $summary
    if($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Unity lab step failed: $log" }
}
Invoke-LabUnity 'UnityWorldSdkSetup.Run' 'world-sdk-setup'
if($Generate) { Invoke-LabUnity 'UnityTrackingLab.Generate' 'lab-generate' }
if($RefineLayout) { Invoke-LabUnity 'UnityTrackingLab.RefineLayout' 'lab-layout' }
if($Check) { Invoke-LabUnity 'UnityTrackingLabChecks.Run' 'lab-check' }
if($Launch) { Invoke-LabUnity 'UnityTrackingLab.BuildAndTestAndroid' 'lab-build' }
else { Invoke-LabUnity 'UnityTrackingLab.BuildAndroid' 'lab-build' }
