$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$sources = @(
    (Join-Path $PSScriptRoot 'OpenXRHandContract.cs'),
    (Join-Path $repo 'Unity/BirdPlugin/Runtime/Scripts/Hand.cs'),
    (Join-Path $repo 'Unity/BirdPlugin/Runtime/Scripts/HandFactory.cs'),
    (Join-Path $repo 'Unity/BirdPlugin/Runtime/XRHands/OpenXRHand.cs')
)
Add-Type -Path $sources -CompilerOptions '/define:BIRD_OPENXR_ENABLED'
[OpenXRHandContract]::Run()
