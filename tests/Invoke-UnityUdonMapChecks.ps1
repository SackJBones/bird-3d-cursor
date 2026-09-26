param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [switch]$Generate,
    [switch]$CompileOnly,
    [switch]$BuildWorld
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
if (!(Test-Path -LiteralPath (Join-Path $project 'Assets/BirdWorld/Scenes/BirdHanoiDemo.unity'))) { throw 'Use the maintained BirdWorld project with its authored Hanoi scene.' }
$runtime=Join-Path $project 'Assets/BirdGenerated/Runtime'
$editor=Join-Path $project 'Assets/BirdGenerated/Editor'
New-Item -ItemType Directory -Force $runtime,$editor | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat') -File | Where-Object { $_.Name -match '\.cs(\.meta)?$' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $runtime $_.Name) }
Get-ChildItem -LiteralPath (Join-Path $repo 'Unity/BirdPlugin/Samples~/HanoiPreview/Resources') -File | Where-Object { $_.Name -match '\.shader(\.meta)?$' } | Copy-Item -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityUdonMapChecks.cs') -Destination $runtime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityWorldBundleChecks.cs') -Destination $editor
function Invoke-MapUnity([string]$Method,[string]$Stem) {
    $result=Join-Path $project ($Stem+'-result.txt'); $log=Join-Path $project ($Stem+'.log')
    Set-Content -LiteralPath $result -Value 'PENDING'
    $process=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-buildTarget','StandaloneWindows64','-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while (!$process.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Map check timed out: $log" } }
    $process.Refresh(); $summary=Get-Content -Raw -LiteralPath $result; Write-Output $summary
    if ($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Map check failed: $log" }
    if (Select-String -LiteralPath $log -Quiet -Pattern 'UdonBehaviour.*exception|Udon runtime exception|An exception occurred during Udon execution') { throw "Udon runtime exception: $log" }
}
if ($CompileOnly) { Invoke-MapUnity 'UnityUdonMapChecks.CompileOnly' 'udon-map-compile'; exit }
if ($Generate) { Invoke-MapUnity 'UnityUdonMapChecks.Generate' 'udon-map-generate' }
Invoke-MapUnity 'UnityUdonMapChecks.Run' 'udon-map'
if ($BuildWorld) {
    Invoke-MapUnity 'UnityWorldBundleChecks.RunMap' 'world-bundle'
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle-result.txt') -Destination (Join-Path $project 'world-map-windows-result.txt')
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle.log') -Destination (Join-Path $project 'world-map-windows.log')
    Invoke-MapUnity 'UnityWorldBundleChecks.AuditMapBundle' 'world-map-bundle-audit'
}
