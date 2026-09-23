param(
    [Parameter(Mandatory = $true)][string]$UnityEditor,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$UnityVersion = '2020.3.33f1'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = [IO.Path]::GetFullPath($ProjectPath)
$marker = Join-Path $project '.bird-generated-validation'
if ((Test-Path $project) -and (!(Test-Path $marker) -or (Get-Content -Raw $marker).Trim() -ne 'Bird Quest deployment smoke project')) {
    throw 'Choose a new directory or the dedicated generated Quest smoke project.'
}
foreach ($folder in @('Assets/Editor', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Force (Join-Path $project $folder) | Out-Null
}
Set-Content $marker 'Bird Quest deployment smoke project'
Set-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') "m_EditorVersion: $UnityVersion"
$manifest = @{dependencies = @{
    'com.bird3d.cursor' = 'file:' + (Join-Path $repo 'Unity/BirdPlugin').Replace('\', '/')
    'com.unity.xr.oculus' = '1.11.2'
    'com.unity.modules.xr' = '1.0.0'
}}
$manifest | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $project 'Packages/manifest.json')
Copy-Item (Join-Path $repo 'Unity/BirdPlugin/Samples~/DesktopPreview/BirdDesktopPreview.cs') (Join-Path $project 'Assets/BirdDesktopPreview.cs')
Copy-Item (Join-Path $PSScriptRoot 'UnityQuestSmoke.cs') (Join-Path $project 'Assets/UnityQuestSmoke.cs')
Copy-Item (Join-Path $PSScriptRoot 'UnityQuestBuild.cs') (Join-Path $project 'Assets/Editor/UnityQuestBuild.cs')
$result = Join-Path $project 'quest-build-result.txt'
Set-Content $result 'PENDING'
$arguments = @('-batchmode', '-nographics', '-buildTarget', 'Android', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'UnityQuestBuild.Run', '-logFile', ('"' + (Join-Path $project 'quest-build.log') + '"'))
$process = Start-Process $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(900000)) { $process.Kill(); throw 'Quest build exceeded 15 minutes; inspect quest-build.log.' }
$process.Refresh()
$summary = Get-Content -Raw $result
Write-Output $summary
if ($process.ExitCode -ne 0 -or !$summary.StartsWith('PASS:')) { throw 'Quest build failed; inspect quest-build.log.' }
Write-Output (Join-Path $project 'Build/BirdQuestSmoke.apk')
