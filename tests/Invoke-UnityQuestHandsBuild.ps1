param(
    [Parameter(Mandatory = $true)][string]$UnityEditor,
    [Parameter(Mandatory = $true)][string]$ProjectPath
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = [IO.Path]::GetFullPath($ProjectPath)
$marker = Join-Path $project '.bird-generated-validation'
if ((Test-Path $project) -and (!(Test-Path $marker) -or (Get-Content -Raw $marker).Trim() -ne 'Bird Quest live hands validation project')) {
    throw 'Choose a new directory or the dedicated generated live hands project.'
}
foreach ($folder in @('Assets/Editor', 'Packages', 'ProjectSettings')) { New-Item -ItemType Directory -Force (Join-Path $project $folder) | Out-Null }
Set-Content $marker 'Bird Quest live hands validation project'
Set-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') 'm_EditorVersion: 2022.3.22f1'
@{dependencies=@{
    'com.bird3d.cursor'='file:' + (Join-Path $repo 'Unity/BirdPlugin').Replace('\','/')
    'com.unity.xr.hands'='1.3.0'
    'com.unity.xr.openxr'='1.10.0'
    'com.unity.modules.xr'='1.0.0'
    'com.unity.modules.androidjni'='1.0.0'
}} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $project 'Packages/manifest.json')

# Bootstrap has no optional adapter references; compile/quit once after adding
# the define. Keep it separate from the real builder so fresh projects work.
@'
using UnityEditor;
public static class BirdHandsBootstrap {
    public static void Run() {
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, "BIRD_OPENXR_ENABLED");
        var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        player.FindProperty("activeInputHandler").intValue = 1;
        player.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}
'@ | Set-Content (Join-Path $project 'Assets/Editor/BirdHandsBootstrap.cs')
function Invoke-BirdUnity([string]$Method, [string]$Log, [bool]$Quit) {
    $arguments=@('-batchmode','-nographics','-buildTarget','Android','-projectPath',('"'+$project+'"'),'-executeMethod',$Method,'-logFile',('"'+(Join-Path $project $Log)+'"'))
    if ($Quit) { $arguments += '-quit' }
    $process=Start-Process $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(900000)) { $process.Kill(); throw "Unity exceeded 15 minutes; see $Log" }
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Unity failed; see $Log" }
}
foreach ($name in @('UnityQuestHands.cs','QuestHandsUdonShim.cs')) { Copy-Item (Join-Path $PSScriptRoot $name) (Join-Path $project "Assets/$name") }
foreach ($name in @('UnityQuestHandsBuild.cs','UnityQuestHandsChecks.cs')) { Copy-Item (Join-Path $PSScriptRoot $name) (Join-Path $project "Assets/Editor/$name") }
foreach ($name in @('BirdSphereFit.cs','BirdCursorState.cs')) { Copy-Item (Join-Path $repo "Integrations/VRChat/$name") (Join-Path $project "Assets/$name") }
Invoke-BirdUnity 'BirdHandsBootstrap.Run' 'hands-configure.log' $true
Set-Content (Join-Path $project 'hands-build-result.txt') 'PENDING'
Set-Content (Join-Path $project 'hands-math-result.txt') 'PENDING'
Invoke-BirdUnity 'UnityQuestHandsBuild.Run' 'hands-build.log' $false
$result = Get-Content -Raw (Join-Path $project 'hands-build-result.txt')
Write-Output $result
if (!$result.StartsWith('PASS:')) { throw 'Live hands build failed; inspect hands-build.log.' }
Write-Output (Join-Path $project 'Build/BirdLiveHands.apk')
