param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [switch]$BuildPlayer
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$project=[IO.Path]::GetFullPath($ProjectPath)
$marker=Join-Path $project '.bird-hanoi-validation'
if ((Test-Path -LiteralPath $project) -and !(Test-Path -LiteralPath $marker)) { throw 'Use a new directory or an existing marked Hanoi validation project.' }
if (!(Test-Path -LiteralPath $UnityEditor)) { throw 'Unity editor executable not found.' }
foreach ($folder in @('','Assets','Assets/Resources','Assets/Editor','Packages','ProjectSettings')) { New-Item -ItemType Directory -Force (Join-Path $project $folder) | Out-Null }
Set-Content -LiteralPath $marker -Value 'Bird Hanoi validation'
Set-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Value 'm_EditorVersion: 2022.3.22f1'
$package=(Join-Path $repo 'Unity/BirdPlugin').Replace('\','/')
@{dependencies=@{'com.bird3d.cursor'="file:$package";'com.unity.modules.imageconversion'='1.0.0'}} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $project 'Packages/manifest.json')
foreach ($name in @('BirdHanoiPreview.cs','BirdHanoiBoard.cs')) { Copy-Item -LiteralPath (Join-Path $repo "Unity/BirdPlugin/Samples~/HanoiPreview/$name") -Destination (Join-Path $project "Assets/$name") }
foreach ($name in @('BirdHanoiSurface.mat','BirdHanoiSurface.mat.meta','BirdHanoiSurface.shader','BirdHanoiSurface.shader.meta')) { Copy-Item -LiteralPath (Join-Path $repo "Unity/BirdPlugin/Samples~/HanoiPreview/Resources/$name") -Destination (Join-Path $project "Assets/Resources/$name") }
foreach ($name in @('UnityHanoiChecks.cs','UnityHanoiEventReceiver.cs','UnityHanoiPlayerSmoke.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $project "Assets/$name") }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityHanoiPlayerBuild.cs') -Destination (Join-Path $project 'Assets/Editor/UnityHanoiPlayerBuild.cs')
function Invoke-HanoiUnity([string]$Method,[string]$Stem) {
    $result=Join-Path $project ($Stem+'-result.txt'); $log=Join-Path $project ($Stem+'.log')
    Set-Content -LiteralPath $result -Value 'PENDING'
    $proc=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-buildTarget','Win64','-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while (!$proc.WaitForExit(1000)) { if([DateTime]::UtcNow -gt $deadline) { $proc.Kill(); throw "Hanoi Unity timeout: $log" } }
    $proc.Refresh(); $summary=Get-Content -Raw -LiteralPath $result; Write-Output $summary
    if($proc.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Hanoi check failed: $log" }
}
Invoke-HanoiUnity 'UnityHanoiChecks.Run' 'hanoi'
if($BuildPlayer) {
    Invoke-HanoiUnity 'UnityHanoiPlayerBuild.Run' 'hanoi-build'
    $result=Join-Path $project 'hanoi-player-result.txt'; $log=Join-Path $project 'hanoi-player.log'
    Set-Content -LiteralPath $result -Value 'PENDING'
    $proc=Start-Process -FilePath (Join-Path $project 'Build/BirdHanoiPreview.exe') -ArgumentList @('-batchmode','-screen-fullscreen','0','-screen-width','1440','-screen-height','1000','-birdHanoiResult',('"'+$result+'"'),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    if(!$proc.WaitForExit(60000)) { $proc.Kill(); throw "Hanoi player timeout: $log" }
    $proc.Refresh(); $summary=Get-Content -Raw -LiteralPath $result; Write-Output $summary
    if($proc.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Hanoi player failed: $log" }
}
