# Focused checks

Run `pwsh -NoProfile -File tests/Test-OpenXRHand.ps1` from the repo root in a fresh PowerShell process.

This compiles the actual Hand and OpenXRHand source with BIRD_OPENXR_ENABLED against deliberately small API doubles. It checks both hands, startup without a subsystem, valid/failed pose reads, shutdown, restart and tracking loss. Pose is a value type, so the original null comparison fails compilation. A failing TryGetPose deliberately returns nonzero data to detect incorrectly consuming its output.

These tests do not validate Unity package resolution, real XR Hands API compatibility, joint mappings, coordinate transformations, Udon, or device behavior. Full Unity compilation and headset checks remain required. No binaries or generated test projects are committed.

## Production solver checks inside Unity

Run the following in PowerShell, adjusting the editor path/version as needed:

```powershell
./tests/Invoke-UnityCoreChecks.ps1 -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2020.3.33f1/Editor/Unity.exe' -UnityVersion '2020.3.33f1' -ProjectPath '../bird-3d-cursor-projects/Validation/Core2020'
```

The runner generates a disposable project in the demo repository, copies the current production Hand/Bird/Kalman sources and the editor checks, and launches Unity in batch mode. It refuses existing directories without its marker and requires both a successful exit and a fresh PASS result. Logs and results remain inside that generated project; do not commit its source copies or Library.

These checks use actual Unity math and production solver code with synthetic input: known sphere center/radius, index-finger clicking, lost tracking during press/release, continued loss and recovery. They establish core editor compilation/execution, not full package import, XR Hands API compatibility, rendering, Udon or device fidelity. A tracking-loss release follows the existing click-up API; consumers requiring explicit cancellation should not interpret it as a confirmed user action.

Batch Kalman-filter checks compare list input against chronological single-measurement updates, including one/multiple samples, oldest/newest-first ordering, noise overrides and the next measurement's result. Empty batches preserve the existing zero return value and leave filter state unchanged.

Invalid-pose checks inject NaN into a fitting joint, positive infinity into the index tip, negative infinity into the thumb base, and an all-zero hand. Each must hold the last valid cursor/geometry, release selection once, clear stale motion and recover without contaminating filter state. These do not yet establish conditioning thresholds for coplanar or nearly singular point sets, or track individual joint validity when an adapter substitutes finite zero coordinates.

## All-source compilation

Add `-AllSources` and use a separate generated project path (for example `../bird-3d-cursor-projects/Validation/AllSources2020`) to compile every production script, including BirdProvider, BirdManager, BirdInteractable and the editor configuration window, then execute the core checks. The runner includes Unity's physics and IMGUI modules. A mode marker prevents accidentally mixing core-only and all-source runs in one project.

Tracking defines are absent in a fresh generated project, so optional Leap/OVR/XR Hands adapter bodies remain excluded. This mode copies scripts into Assets: it is **not** a UPM installation test, player build, enabled tracking-adapter compile or live behavior test for provider/interactable components. Keep the generated project dedicated to this runner rather than changing defines or copying unrelated scripts into it.

## UPM installation

Use `-Package` instead of `-AllSources`, with a new project directory such as `../bird-3d-cursor-projects/Validation/Package2020`. The generated manifest references Unity/BirdPlugin via a local file dependency, and no production scripts are copied into Assets. Core tests run against Bird3D.Runtime and assert that Bird3D.Editor is separately loaded. The base package supplies physics and IMGUI dependencies; no hand SDK or tracking symbols are required. This validates UPM import and editor compilation, not player builds or enabled hardware adapters. See Unity/BirdPlugin/README.md for the current migration limits.

## Standalone player build and startup

Use `-Package -BuildPlayer` with a separate directory, for example `../bird-3d-cursor-projects/Validation/PackagePlayer2020`, and the same editor/version parameters. This builds a Windows x64 Mono player with a generated smoke-test scene, checks that Bird3D.Runtime.dll is included and Bird3D.Editor.dll is excluded, then runs the player headlessly. The player instantiates Bird with untracked synthetic input and checks idle state and runtime assembly loading without UnityEditor assemblies.

Both build and player must exit successfully and write fresh PASS results. Build/import has a three-minute timeout and the player a thirty-second timeout. Artifacts and logs remain in the generated project; this is local validation output, not a release build. This mode does not rerun the 63 editor checks, exercise rendering or provider/interactable scene behavior, or validate IL2CPP, Android, XR hardware or VRChat.
