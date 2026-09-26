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
if (!(Test-Path -LiteralPath (Join-Path $project 'Assets/BirdWorld/Scenes/BirdFeasibility.unity'))) {
    throw 'Use the maintained BirdWorld project with its Worlds SDK and feasibility scene.'
}
if (!(Test-Path -LiteralPath $UnityEditor)) { throw 'Unity editor executable not found.' }
$runtime=Join-Path $project 'Assets/BirdGenerated/Runtime'
$editor=Join-Path $project 'Assets/BirdGenerated/Editor'
New-Item -ItemType Directory -Force $runtime,$editor | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'Integrations/VRChat') -File | Where-Object { $_.Name -match '\.cs(\.meta)?$' } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $runtime $_.Name)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityUdonUiChecks.cs') -Destination (Join-Path $runtime 'UnityUdonUiChecks.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityWorldBundleChecks.cs') -Destination (Join-Path $editor 'UnityWorldBundleChecks.cs')
function Invoke-UiUnity([string]$Method,[string]$Stem,[string]$Target='StandaloneWindows64') {
    $result=Join-Path $project ($Stem+'-result.txt')
    $log=Join-Path $project ($Stem+'.log')
    Set-Content -LiteralPath $result -Value 'PENDING'
    $arguments=@('-batchmode','-buildTarget',$Target,'-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+$log+'"'))
    $process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while (!$process.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Unity UI check timed out. See $log" }
    }
    $process.Refresh()
    $summary=Get-Content -Raw -LiteralPath $result
    Write-Output $summary
    if ($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Unity UI check failed. See $log" }
    if (Select-String -LiteralPath $log -Quiet -Pattern 'UdonBehaviour.*exception|Udon runtime exception|An exception occurred during Udon execution') {
        throw "Udon runtime exception recorded despite result. See $log"
    }
}
if ($Generate) { Invoke-UiUnity 'UnityUdonUiChecks.Generate' 'udon-ui-generate' }
Invoke-UiUnity 'UnityUdonUiChecks.Run' 'udon-ui'
if ($BuildWorld) {
    Invoke-UiUnity 'UnityWorldBundleChecks.RunUi' 'world-bundle'
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle-result.txt') -Destination (Join-Path $project 'world-ui-windows-result.txt')
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle.log') -Destination (Join-Path $project 'world-ui-windows.log')
}
if ($BuildAndroidWorld) {
    Invoke-UiUnity 'UnityWorldSdkSetup.Run' 'world-sdk-setup' 'Android'
    Invoke-UiUnity 'UnityWorldBundleChecks.RunUiAndroid' 'world-bundle' 'Android'
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle-result.txt') -Destination (Join-Path $project 'world-ui-android-result.txt')
    Copy-Item -LiteralPath (Join-Path $project 'world-bundle.log') -Destination (Join-Path $project 'world-ui-android.log')
}
