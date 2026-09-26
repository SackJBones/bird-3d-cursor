param(
    [Parameter(Mandatory = $true)][string]$UnityEditor,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$UnityVersion = '2022.3.22f1',
    [switch]$BuildPlayer
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = [IO.Path]::GetFullPath($ProjectPath)
$marker = Join-Path $project '.bird-menu-validation'
if ((Test-Path -LiteralPath $project) -and !(Test-Path -LiteralPath $marker)) {
    throw 'Use a new directory or an existing project with the Bird menu validation marker.'
}
if (!(Test-Path -LiteralPath $UnityEditor)) { throw 'Unity editor executable not found.' }
foreach ($folder in @('', 'Assets', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Force (Join-Path $project $folder) | Out-Null
}
Set-Content -LiteralPath $marker -Value 'Bird menu validation'
Set-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Value "m_EditorVersion: $UnityVersion"
$package = (Join-Path $repo 'Unity/BirdPlugin').Replace('\','/')
$manifest = @{ dependencies = @{ 'com.bird3d.cursor' = "file:$package"; 'com.unity.modules.imageconversion' = '1.0.0' } }
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $project 'Packages/manifest.json')
Copy-Item -LiteralPath (Join-Path $repo 'Unity/BirdPlugin/Samples~/MenuPreview/BirdMenuPreview.cs') -Destination (Join-Path $project 'Assets/BirdMenuPreview.cs')
New-Item -ItemType Directory -Force (Join-Path $project 'Assets/Resources') | Out-Null
foreach ($name in @('BirdMenuPreviewSurface.mat', 'BirdMenuPreviewSurface.mat.meta')) {
    Copy-Item -LiteralPath (Join-Path $repo "Unity/BirdPlugin/Samples~/MenuPreview/Resources/$name") -Destination (Join-Path $project "Assets/Resources/$name")
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityMenuChecks.cs') -Destination (Join-Path $project 'Assets/UnityMenuChecks.cs')
$result = Join-Path $project 'menu-result.txt'
$log = Join-Path $project 'menu-checks.log'
Set-Content -LiteralPath $result -Value 'PENDING: Unity has not completed this run.'
# A real graphics device is required for the pixel controls. The fixture exits Unity itself.
$arguments = @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'UnityMenuChecks.Run', '-logFile', ('"' + $log + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddMinutes(4)
while (!$process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Unity menu checks timed out. See $log" }
}
$process.Refresh()
$summary = Get-Content -Raw -LiteralPath $result
Write-Output $summary
if ($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) {
    throw "Unity menu checks failed (exit $($process.ExitCode)). See $log"
}
Write-Output "Interactive scene: $project/Assets/MenuPreview.unity"
Write-Output "Camera captures: $project/MenuCaptures"
if ($BuildPlayer) {
    New-Item -ItemType Directory -Force (Join-Path $project 'Assets/Editor') | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityMenuPlayerBuild.cs') -Destination (Join-Path $project 'Assets/Editor/UnityMenuPlayerBuild.cs')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityMenuPlayerSmoke.cs') -Destination (Join-Path $project 'Assets/UnityMenuPlayerSmoke.cs')
    $buildResult = Join-Path $project 'menu-build-result.txt'
    $buildLog = Join-Path $project 'menu-build.log'
    Set-Content -LiteralPath $buildResult -Value 'PENDING'
    $arguments = @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'UnityMenuPlayerBuild.Run', '-logFile', ('"' + $buildLog + '"'))
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddMinutes(8)
    while (!$process.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Menu player build timed out. See $buildLog" }
    }
    $process.Refresh()
    $summary = Get-Content -Raw -LiteralPath $buildResult
    Write-Output $summary
    if ($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Menu player build failed. See $buildLog" }
    $playerResult = Join-Path $project 'menu-player-result.txt'
    $playerLog = Join-Path $project 'menu-player.log'
    Set-Content -LiteralPath $playerResult -Value 'PENDING'
    $arguments = @('-batchmode', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '800', '-birdMenuResult', ('"' + $playerResult + '"'), '-logFile', ('"' + $playerLog + '"'))
    $process = Start-Process -FilePath (Join-Path $project 'Build/BirdMenuPreview.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(30000)) { $process.Kill(); throw "Menu player timed out. See $playerLog" }
    $process.Refresh()
    $summary = Get-Content -Raw -LiteralPath $playerResult
    Write-Output $summary
    if ($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw "Menu player smoke failed. See $playerLog" }
}
