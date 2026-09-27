param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [ValidateSet('Android','Windows','Both')][string]$Platform='Android',
    [switch]$Generate,
    [switch]$RefineLayout,
    [switch]$RefineBird,
    [switch]$AddBird,
    [switch]$AddUi,
    [switch]$RefineUi,
    [switch]$Check,
    [switch]$CheckBird,
    [switch]$SkipBuild,
    [switch]$Launch
)
$ErrorActionPreference='Stop'
if($SkipBuild -and $Launch) { throw 'Choose SkipBuild or Launch, not both.' }
if($Platform -eq 'Both' -and $Launch) { throw 'Launch one selected platform at a time.' }
$repo=Split-Path $PSScriptRoot -Parent
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
if(!(Test-Path -LiteralPath (Join-Path $project 'Assets/BirdWorld/Scenes/BirdFeasibility.unity'))) { throw 'Use the maintained BirdWorld project.' }
if(!(Test-Path -LiteralPath $UnityEditor)) { throw 'Unity executable missing.' }
$runtime=Join-Path $project 'Assets/BirdGenerated/Runtime'; $editor=Join-Path $project 'Assets/BirdGenerated/Editor'
New-Item -ItemType Directory -Force $runtime,$editor | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat') -File | Where-Object { $_.Name -match '\.cs(\.meta)?$' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $runtime $_.Name) }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityTrackingLabChecks.cs') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityAvatarHandLabChecks.cs') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityAvatarUiLabChecks.cs') -Destination $runtime
foreach($helper in @('UnityTrackingLab.cs','UnityTrackingLabBird.cs','UnityTrackingLabUi.cs','UnityTrackingLabBuildAudit.cs','UnityWorldBundleChecks.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $helper) -Destination $editor }
$presentation=Join-Path $project 'Assets/BirdGenerated/Presentation'
New-Item -ItemType Directory -Force $presentation | Out-Null
foreach($name in @('BirdLogicalDepth.shader','BirdLogicalDepth.shader.meta')) { Copy-Item -LiteralPath (Join-Path $repo ('Unity/BirdPlugin/Runtime/Presentation/'+$name)) -Destination $presentation }
foreach($name in @('BirdLabXRay.shader','BirdLabXRay.shader.meta')) { Copy-Item -LiteralPath (Join-Path $repo ('Integrations/VRChat/Shaders/'+$name)) -Destination $presentation }
function Invoke-LabUnity([string]$Method,[string]$Stem,[string]$Target) {
    $result=Join-Path $project ($Stem+'-result.txt'); $log=Join-Path $project ($Stem+'.log')
    Set-Content -LiteralPath $result -Value 'PENDING'
    $process=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-buildTarget',$Target,'-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    if(!$process.WaitForExit(600000)) { $process.Kill(); throw "Unity lab step timed out: $log" }
    $process.Refresh(); $summary=Get-Content -Raw -LiteralPath $result; Write-Output $summary
    if($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Unity lab step failed: $log" }
    if(Select-String -LiteralPath $log -Quiet -Pattern 'UdonBehaviour.*exception|Udon runtime exception|An exception occurred during Udon execution') { throw "Udon execution error: $log" }
    Copy-Item -LiteralPath $result -Destination (Join-Path $project ($Stem+'-'+$Target+'-result.txt'))
    Copy-Item -LiteralPath $log -Destination (Join-Path $project ($Stem+'-'+$Target+'.log'))
}
$targets=if($Platform -eq 'Both') { @('StandaloneWindows64','Android') } elseif($Platform -eq 'Windows') { @('StandaloneWindows64') } else { @('Android') }
$author=$true
foreach($target in $targets) {
    Invoke-LabUnity 'UnityWorldSdkSetup.Run' 'world-sdk-setup' $target
    if($author) {
        if($Generate) { Invoke-LabUnity 'UnityTrackingLab.Generate' 'lab-generate' $target }
        if($RefineLayout) { Invoke-LabUnity 'UnityTrackingLab.RefineLayout' 'lab-layout' $target }
        if($RefineBird) { Invoke-LabUnity 'UnityTrackingLabBird.RefineBird' 'lab-bird-layout' $target }
        if($AddBird) { Invoke-LabUnity 'UnityTrackingLabBird.AddBird' 'lab-bird-author' $target }
        if($AddUi) { Invoke-LabUnity 'UnityTrackingLabUi.AddUi' 'lab-ui-author' $target }
        if($RefineUi) { Invoke-LabUnity 'UnityTrackingLabUi.RefineUi' 'lab-ui-layout' $target }
        $author=$false
    }
    if($Check) { Invoke-LabUnity 'UnityTrackingLabChecks.Run' 'lab-check' $target }
    if($CheckBird) { Invoke-LabUnity 'UnityAvatarHandLabChecks.Run' 'lab-hand-check' $target }
    if(!$SkipBuild) {
        if($Launch) { Invoke-LabUnity 'UnityTrackingLab.BuildAndTestCurrent' 'lab-build' $target }
        else { Invoke-LabUnity 'UnityTrackingLab.BuildCurrent' 'lab-build' $target }
    }
}
if($Platform -eq 'Both' -and !$SkipBuild) {
    Invoke-LabUnity 'UnityTrackingLabBuildAudit.ComparePlatforms' 'lab-platforms' 'Android'
}
