param(
    [Parameter(Mandatory=$true)][string]$UnityEditor,
    [Parameter(Mandatory=$true)][string]$ProjectPath,
    [switch]$Generate,
    [switch]$BuildWorld,
    [switch]$BuildAndroidWorld
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
if (!(Test-Path -LiteralPath (Join-Path $project 'Assets/BirdWorld/Scenes/BirdUiDemo.unity'))) { throw 'Use the maintained BirdWorld project with its authored UI scene.' }
if (!(Test-Path -LiteralPath $UnityEditor)) { throw 'Unity editor executable not found.' }
$runtime=Join-Path $project 'Assets/BirdGenerated/Runtime'
$editor=Join-Path $project 'Assets/BirdGenerated/Editor'
New-Item -ItemType Directory -Force $runtime,$editor | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat') -File | Where-Object { $_.Name -match '\.cs(\.meta)?$' } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $runtime $_.Name)
}
Get-ChildItem -LiteralPath (Join-Path $repo 'Unity/BirdPlugin/Samples~/HanoiPreview/Resources') -File | Where-Object { $_.Name -match '\.shader(\.meta)?$' } | Copy-Item -Destination $runtime
foreach ($helper in @('UnityUdonUiChecks.cs','UnityUdonHanoiChecks.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $helper) -Destination (Join-Path $runtime $helper) }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityWorldBundleChecks.cs') -Destination (Join-Path $editor 'UnityWorldBundleChecks.cs')
function Invoke-HanoiUnity([string]$Method,[string]$Stem,[string]$Target='StandaloneWindows64') {
    $result=Join-Path $project ($Stem+'-result.txt')
    $log=Join-Path $project ($Stem+'.log')
    Set-Content -LiteralPath $result -Value 'PENDING'
    $arguments=@('-batchmode','-buildTarget',$Target,'-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"'))
    $process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while (!$process.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Unity Hanoi check timed out. See $log" }
    }
    $process.Refresh()
    $summary=Get-Content -Raw -LiteralPath $result
    Write-Output $summary
    if ($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Unity Hanoi check failed. See $log" }
    if (Select-String -LiteralPath $log -Quiet -Pattern 'UdonBehaviour.*exception|Udon runtime exception|An exception occurred during Udon execution') { throw "Udon runtime exception recorded despite result. See $log" }
}
if ($Generate) { Invoke-HanoiUnity 'UnityUdonHanoiChecks.Generate' 'udon-hanoi-generate' }
Invoke-HanoiUnity 'UnityUdonHanoiChecks.Run' 'udon-hanoi'
Invoke-HanoiUnity 'UnityUdonUiChecks.Run' 'udon-ui'
if ($BuildWorld) {
    Invoke-HanoiUnity 'UnityWorldBundleChecks.RunHanoi' 'world-bundle'
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle-result.txt') -Destination (Join-Path $project 'world-hanoi-windows-result.txt')
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle.log') -Destination (Join-Path $project 'world-hanoi-windows.log')
}
if ($BuildAndroidWorld) {
    Invoke-HanoiUnity 'UnityWorldSdkSetup.Run' 'world-sdk-setup' 'Android'
    Invoke-HanoiUnity 'UnityWorldBundleChecks.RunHanoiAndroid' 'world-bundle' 'Android'
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle-result.txt') -Destination (Join-Path $project 'world-hanoi-android-result.txt')
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle.log') -Destination (Join-Path $project 'world-hanoi-android.log')
}
