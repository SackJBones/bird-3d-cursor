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
