# Local Udon manipulation and paired Hanoi

`Integrations/VRChat` adapts the [ordinary Unity manipulation contract](OBJECT-MANIPULATION.md)
into local Udon components. The heavy BirdWorld project's `BirdHanoiDemo` scene
places a tabletop puzzle on a viewing terrace and full-size building sections
420 m away. Both use the same grip, workspace and snap-target programs. The
existing nested menu and back-surface color selector remain available to the left.

The scene currently uses **labeled desktop demonstration input**. It does not
enable the experimental avatar-bone adapter, claim usable VRChat finger input,
or update the standalone Quest app. Geometry, range, filtering, clicking and
Plain Inflate remain separate and unchanged.

## Components

| Component | Purpose |
| --- | --- |
| `BirdObjectGrip` | Either hand may acquire one local transaction; preserve pickup offset, follow bounded translation, guide, place or return |
| `BirdObjectTarget` | Explicit box volume, workspace, policy, destinations and named local events |
| `BirdObjectRegion` | Workspace coordinates and feasible pivot bounds for every corner of the target box |
| `BirdObjectSnapTarget` | Approach segment, soft attraction, capture radius and exit hysteresis |
| `BirdObjectPolicy` | Synchronous local Udon event bridge for experience-specific rules |
| `BirdObjectPlayArea` | Local-player position and conservative viewing/workspace separation gate |
| `BirdObjectFeedback` | Independent optional renderer tint, restored on disable/rebind |
| `BirdHanoiBoard` | Top-piece reservation, legal stack placement, move count and completion |
| `BirdHanoiStation` | Sample reset/cancel commands, placement cues and instructions |

These are ordinary Inspector-configured UdonSharp components with stable script
metadata. They deliberately do not inherit the ordinary C# abstract placement
rule or attempt to execute package MonoBehaviours as world scripts. Programs
use `BehaviourSyncMode.None`: ownership and puzzle state are local to each client.

Configure a policy's `evaluator` and `evaluationEvent`. Its evaluator reads the
bridge's `operation`, `item` and `destination`, then writes `allowed` synchronously.
The default is rejection. `CanGrab` and `CanPlace` must not mutate committed state;
`Begin` reserves; `Commit` validates and changes state atomically; `Cancel` releases
the reservation. Cancellation is allowed to reach a disabled evaluator for cleanup.
The bridge rejects recursive queries. Target notifications are separate named
local events, sent after the grip's corresponding state transition. Hanoi uses
them for simple station counters.

## Input and placement

`BirdUiPointer` carries accepted logical origin/point/press samples. Submit before
the UI router at execution order 100 and the grip at 120. The optional cursor
adapter runs at 50; sample cues and renderer feedback run at 140/150.
The sample registers both hands and all six objects with one grip controller.
Multiple controllers consuming the same stream would need shared arbitration.

Accepted UI contact marks `uiConsumed` on that sample. Invoking a control also
marks it before callbacks, so a close action cannot reuse its press to pick up
an object behind it. A fresh `Submit` resets the claim. A registered foreground
menu blocks world gripping across the scene, including the other hand; opening
it during manipulation cancels the grip. UI hit testing does not perform general
scene occlusion: register and arrange the intended interaction surfaces explicitly.

Pickup preserves the displacement between Bird's logical endpoint and the object
pivot. This works when the point is far beyond the selected collider. All eight
corners of the configured box remain inside its authored workspace. The point
itself is never clamped. Guidance attracts toward an eligible approach segment;
a wider exit radius retains its candidate. Release commits only when the **raw**
desired pivot, the displayed pivot, the exact endpoint's whole-volume bounds and
the experience rule all permit placement.

Invalid release, tracking loss, changed user, explicit cancellation or a pause
above 250 ms returns to the committed pose. Disable rolls back immediately.
The item remains owned during animated return. Reset is blocked while pieces
are owned; the station's reset command cancels first. Held recovery cannot create
a new pickup edge. A fresh press from the other hand remains usable.

The desktop producer uses look direction, exponential wheel reach up to 1500 m,
mouse hold/release and X to cancel. That reach setting belongs only to this demo
producer; it is not a geometric Bird range cap. Existing UI-only desktop defaults
remain linear wheel reach up to 30 m.

## Viewing area and scale

The tabletop uses 0.2 m per workspace unit: 6.8/8.4/10 cm sections. Buildings use
75 m per unit: 25.5/31.5/37.5 m sections. This is a 375-fold scale difference.
The remote workspace's front boundary is 322.5 m forward. Closing the hand cannot
bring any part of a held building across that boundary.

The scene has a small collidable terrace and boundary walls. `BirdObjectPlayArea`
rejects missing/invalid player data, positions outside its box, and any protected
workspace whose world AABB overlaps the viewing box. Leaving the area cancels
manipulation; it does **not** teleport the player. Only the building workspace is
protected, since the tabletop is intentionally within reach. Do not add a walk or
teleport destination inside the remote workspace and assume this is general
occupancy avoidance. The AABB test is conservative for rotated areas.

This slice does not handle obstacle-aware lifting, collision-free drag paths,
dynamic obstacles, arbitrary free-space drops, object rotation/scaling or shared
network reservations. Physical grip/snapping feel, VRChat-client loading, device
performance and final world design remain separate work.

## Reproduction

From the light repository, with Unity 2022.3.22f1 and the heavy project's installed
Worlds SDK 3.10.5:

```powershell
./tests/Invoke-UnityUdonHanoiChecks.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/BirdWorld' -BuildWorld -BuildAndroidWorld
```

The runner restores integration source/meta pairs and the sample surface shader
into ignored `Assets/BirdGenerated`, compiles Udon and reopens the saved scene.
Runtime fixtures drive backing VM heaps/events; they never call a runtime C#
proxy method. Proxies are used only to author scenes. `-Generate` creates a missing
scene and refuses to overwrite one. The existing UI suite also runs as a regression.
Optional builds use the official SDK build-only API with an ignored scene copy,
without upload or client launch. The last requested platform stays selected.

Captures and bundles are generated under ignored `Validation/UdonHanoi` and
`Validation/WorldBuild`. Execution logs/result files remain in BirdWorld. See the
latest checkpoint for measured results and any SDK-build qualifications.

## Measured checkpoint, 2026-09-26

Unity 2022.3.22f1 / Worlds SDK 3.10.5 passes **877 compiled-Udon assertions**
in the saved Hanoi scene, including a normal Update/LateUpdate sequence and seven
D3D11 camera captures. Counts include individual transformed-volume corner checks.
The existing UI suite also passes **441 assertions**, its normal-frame selector
sequence and four captures. The new reset action is exercised through the authored
UI, after both puzzles are solved. Renderer checks include enclosing the actual
body/window geometry and separating pad tops from the table surface.

The [compiled translation measurements](measurements/udon-manipulation-translation-rates.csv)
use a one-second linear target and supplied 30/72/120 Hz intervals. Maximum final
position difference from 120 Hz is below 0.000002 workspace units. This measures
the implemented response under synthetic intervals, not real device pacing or feel.

The first VM run exposed a value-type mutation difference: `Bounds.SetMinMax`
did not retain the intended Udon value. Assigning an explicit `new Bounds(center,
size)` fixed both feasible-pivot computation and the viewing-area overlap test.
The resulting code passes normal-frame movement and transformed-volume checks;
ordinary C# success alone would not have exposed this port issue.

The SDK build-only API produced these final scene files under ignored
`Validation/WorldBuild`:

| Target | File | Bytes | SHA256 |
| --- | --- | ---: | --- |
| Windows x64 | BirdHanoiDemo.vrcw | 236456 | `C716BC36C70F450F935D0461570C784614B2F2F450532D1F709EDFBBCA0509BB` |
| Android | BirdHanoiDemo_Android.vrcw | 197551 | `2C94C01A18C1B888A37EA88D5C225A3C4D5E1EDBA6BAF634ED199DAEFD6FEB11` |

`UnityWorldBundleChecks.AuditHanoiBundles` independently loads both catalogs and
finds the expected `Assets/BirdGenerated/BirdHanoiDemoBuildValidation.unity` scene.
It does not instantiate either bundled scene.

Both SDK logs still contain Unity's internal `Build Finished, Result: Failure.`
line, despite SDK API completion and fresh output. The cause remains unresolved;
no corresponding C# compiler error, shader error or Udon runtime exception was
found in these runs. This is the same qualification as the preceding UI-world
checkpoint. Do not treat these files as clean VRChat-client or mobile-runtime
validation. No upload, headset operation or account action occurred.
