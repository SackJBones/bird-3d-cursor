param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [switch]$Author,
    [switch]$BuildWorld
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
if (!(Test-Path -LiteralPath (Join-Path $project 'Assets/BirdWorld/Scenes/BirdMapDemo.unity'))) { throw 'Use the maintained BirdWorld project with its authored map scene.' }
$runtime=Join-Path $project 'Assets/BirdGenerated/Runtime'; $editor=Join-Path $project 'Assets/BirdGenerated/Editor'
New-Item -ItemType Directory -Force $runtime,$editor | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat') -File | Where-Object { $_.Name -match '\.cs(\.meta)?$' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $runtime $_.Name) }
Get-ChildItem -LiteralPath (Join-Path $repo 'Unity/BirdPlugin/Samples~/HanoiPreview/Resources') -File | Where-Object { $_.Name -match '\.shader(\.meta)?$' } | Copy-Item -Destination $runtime
foreach($name in @('UnityUdonVisualChecks.cs','UnityUdonMapChecks.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $runtime }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityWorldBundleChecks.cs') -Destination $editor
function Invoke-VisualUnity([string]$Method,[string]$Stem) {
    $result=Join-Path $project ($Stem+'-result.txt'); $log=Join-Path $project ($Stem+'.log'); Set-Content -LiteralPath $result -Value 'PENDING'
    $process=Start-Process -FilePath $UnityEditor -ArgumentList @('-batchmode','-buildTarget','StandaloneWindows64','-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while (!$process.WaitForExit(1000)) { if([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Visual check timeout: $log" } }
    $process.Refresh(); $summary=Get-Content -Raw -LiteralPath $result; Write-Output $summary
    if($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Visual check failed: $log" }
    if(Select-String -LiteralPath $log -Quiet -Pattern 'UdonBehaviour.*exception|Udon runtime exception|An exception occurred during Udon execution') { throw "Udon runtime exception: $log" }
}
if($Author) {
    $baseline=Join-Path $project 'Assets/BirdGenerated/BirdMapBeforeVisual.unity'
    if(!(Test-Path -LiteralPath $baseline)) { Copy-Item -LiteralPath (Join-Path $project 'Assets/BirdWorld/Scenes/BirdMapDemo.unity') -Destination $baseline }
    Invoke-VisualUnity 'UnityUdonVisualChecks.Author' 'udon-visual-author'
}
Invoke-VisualUnity 'UnityUdonVisualChecks.Run' 'udon-visual'
Invoke-VisualUnity 'UnityUdonMapChecks.Run' 'udon-map'
if($BuildWorld) {
    Invoke-VisualUnity 'UnityWorldBundleChecks.RunMap' 'world-bundle'
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle-result.txt') -Destination (Join-Path $project 'world-map-windows-result.txt')
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle.log') -Destination (Join-Path $project 'world-map-windows.log')
    Invoke-VisualUnity 'UnityWorldBundleChecks.AuditMapBundle' 'world-map-bundle-audit'
}
