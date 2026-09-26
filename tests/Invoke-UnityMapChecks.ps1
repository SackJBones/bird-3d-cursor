param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [switch]$BuildPlayer
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$project=[IO.Path]::GetFullPath($ProjectPath)
$marker=Join-Path $project '.bird-map-validation'
if ((Test-Path -LiteralPath $project) -and !(Test-Path -LiteralPath $marker)) { throw 'Use a new directory or an existing marked map validation project.' }
if (!(Test-Path -LiteralPath $UnityEditor)) { throw 'Unity editor executable not found.' }
foreach ($folder in @('','Assets','Assets/Resources','Assets/Editor','Packages','ProjectSettings')) { New-Item -ItemType Directory -Force (Join-Path $project $folder) | Out-Null }
Set-Content -LiteralPath $marker -Value 'Bird map validation'
Set-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Value 'm_EditorVersion: 2022.3.22f1'
$package=(Join-Path $repo 'Unity/BirdPlugin').Replace('\','/')
@{dependencies=@{'com.bird3d.cursor'="file:$package";'com.unity.modules.imageconversion'='1.0.0'}} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $project 'Packages/manifest.json')
Get-ChildItem -LiteralPath (Join-Path $repo 'Unity/BirdPlugin/Samples~/MenuPreview') -File | Where-Object { $_.Name -match '\.cs(\.meta)?$' } | Copy-Item -Destination (Join-Path $project 'Assets')
Copy-Item -Path (Join-Path $repo 'Unity/BirdPlugin/Samples~/MenuPreview/Resources/*') -Destination (Join-Path $project 'Assets/Resources')
foreach ($name in @('UnityMapChecks.cs','UnitySphericalScrollChecks.cs','UnityMapPlayerSmoke.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $project "Assets/$name") }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityMapPlayerBuild.cs') -Destination (Join-Path $project 'Assets/Editor/UnityMapPlayerBuild.cs')
function Invoke-MapUnity([string]$Method,[string]$Stem) {
    $result=Join-Path $project ($Stem+'-result.txt'); $log=Join-Path $project ($Stem+'.log')
    Set-Content -LiteralPath $result -Value 'PENDING'
    $proc=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-buildTarget','Win64','-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while (!$proc.WaitForExit(1000)) { if([DateTime]::UtcNow -gt $deadline) { $proc.Kill(); throw "Map Unity timeout: $log" } }
    $proc.Refresh(); $summary=Get-Content -Raw -LiteralPath $result; Write-Output $summary
    if($proc.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Map check failed: $log" }
}
Invoke-MapUnity 'UnityMapChecks.Run' 'map'
if($BuildPlayer) {
    Invoke-MapUnity 'UnityMapPlayerBuild.Run' 'map-build'
    $result=Join-Path $project 'map-player-result.txt'; $log=Join-Path $project 'map-player.log'
    Set-Content -LiteralPath $result -Value 'PENDING'
    $proc=Start-Process -FilePath (Join-Path $project 'Build/BirdMapPreview.exe') -ArgumentList @('-batchmode','-screen-fullscreen','0','-screen-width','1400','-screen-height','1000','-birdMapResult',('"'+$result+'"'),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    if(!$proc.WaitForExit(60000)) { $proc.Kill(); throw "Map player timeout: $log" }
    $proc.Refresh(); $summary=Get-Content -Raw -LiteralPath $result; Write-Output $summary
    if($proc.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Map player failed: $log" }
}
