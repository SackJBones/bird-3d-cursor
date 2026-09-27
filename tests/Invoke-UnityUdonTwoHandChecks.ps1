param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [ValidateSet('Windows','Android','Both')][string]$Platform='Windows',
    [switch]$Author,
    [switch]$Regressions,
    [switch]$BuildWorld
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
[string[]]$selectedTargets=if($Platform -eq 'Both') { @('StandaloneWindows64','Android') } elseif($Platform -eq 'Android') { @('Android') } else { @('StandaloneWindows64') }
if (!(Test-Path -LiteralPath (Join-Path $project 'Assets/BirdWorld/Scenes/BirdPoseDemo.unity'))) { throw 'Use maintained BirdWorld with its authored pose scene.' }
if (!(Test-Path -LiteralPath $UnityEditor)) { throw 'Unity editor executable not found.' }
$runtime=Join-Path $project 'Assets/BirdGenerated/Runtime'; $editor=Join-Path $project 'Assets/BirdGenerated/Editor'
New-Item -ItemType Directory -Force $runtime,$editor | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat') -File | Where-Object { $_.Name -match '\.cs(\.meta)?$' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $runtime $_.Name) }
foreach($helper in @('UnityUdonTwoHandChecks.cs','UnityUdonPoseChecks.cs','UnityUdonHanoiChecks.cs','UnityUdonMapChecks.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $helper) -Destination $runtime }
Get-ChildItem -LiteralPath (Join-Path $repo 'Unity/BirdPlugin/Samples~/HanoiPreview/Resources') -File | Where-Object { $_.Name -match '\.shader(\.meta)?$' } | Copy-Item -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityWorldBundleChecks.cs') -Destination $editor
function Invoke-TwoHandUnity([string]$Method,[string]$Stem,[string]$Target) {
    $result=Join-Path $project ($Stem+'-result.txt'); $log=Join-Path $project ($Stem+'.log'); Set-Content -LiteralPath $result -Value 'PENDING'
    $process=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-buildTarget',$Target,'-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while (!$process.WaitForExit(1000)) { if([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Two-hand check timeout: $log" } }
    $process.Refresh(); $summary=Get-Content -Raw -LiteralPath $result; Write-Output $summary
    if($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Two-hand check failed: $log" }
    if(Select-String -LiteralPath $log -Quiet -Pattern 'UdonBehaviour.*exception|Udon runtime exception|An exception occurred during Udon execution') { throw "Udon runtime exception: $log" }
    Copy-Item -LiteralPath $result -Destination (Join-Path $project ($Stem+'-'+$Target+'-result.txt'))
    Copy-Item -LiteralPath $log -Destination (Join-Path $project ($Stem+'-'+$Target+'.log'))
}
if($Author) { Invoke-TwoHandUnity 'UnityUdonTwoHandChecks.Author' 'udon-two-hand-author' $selectedTargets[0] }
foreach($target in $selectedTargets) {
    Invoke-TwoHandUnity 'UnityUdonTwoHandChecks.Run' 'udon-two-hand' $target
    if($Regressions) {
        Invoke-TwoHandUnity 'UnityUdonPoseChecks.Run' 'udon-pose' $target
        Invoke-TwoHandUnity 'UnityUdonHanoiChecks.RunPose' 'udon-hanoi' $target
        Invoke-TwoHandUnity 'UnityUdonMapChecks.RunPose' 'udon-map' $target
    }
    if($BuildWorld) {
        $method=if($target -eq 'Android') { 'UnityWorldBundleChecks.RunPoseAndroid' } else { 'UnityWorldBundleChecks.RunPose' }
        $audit=if($target -eq 'Android') { 'UnityWorldBundleChecks.AuditPoseAndroidBundle' } else { 'UnityWorldBundleChecks.AuditPoseBundle' }
        $auditStem=if($target -eq 'Android') { 'world-pose-android-audit' } else { 'world-pose-bundle-audit' }
        Invoke-TwoHandUnity $method 'world-bundle' $target
        Invoke-TwoHandUnity $audit $auditStem $target
    }
}
