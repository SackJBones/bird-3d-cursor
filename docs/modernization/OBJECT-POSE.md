# Optional held rotation, size and pose docking

The ordinary Unity manipulation module now supports opt-in rotation and uniform
scaling within the existing grab transaction. Hanoi leaves both options off.
The new **BirdPosePreview** in the Hanoi Preview sample demonstrates two docks
at tabletop and building scale: the second requires a 90-degree turn and 1.25x
size. The local Udon components implement the same optional pose contract in
the separate authored `BirdPoseDemo` world scene described below.

Bird continues to specify a point. The host separately chooses how to request
orientation and size. The sample uses keyboard commands while a logical Bird
press holds the object. It does not infer a hand orientation from the point or
impose a gesture on the geometric solver. Sphere fitting, range, filtering,
click detection and the installed Quest v0.9 application are unchanged.

## Authoring

Enable **Allow Rotation** and/or **Allow Scaling** on `BirdGrabTarget`. After
sizing the object, use **Capture Pose Reference Scale** in its component context
menu. That serialized local scale defines factor 1 across all later placements;
the reference is not replaced on every pickup. Positive factors preserve the
authored proportions and any mirrored axes. Configure positive minimum/maximum
factors, and keep the object inside that range. `ConfigurePose` captures the
reference and requires its supplied limits to contain 1. Configure only between
transactions; changes during a grip cancel it.

The target still needs a box enclosing its relevant geometry and an authored
`BirdPlacementRegion`. It remains kinematic, with no required Rigidbody. Simple
offset/rotated child boxes and mirrored/nonuniform parent scales are supported.
Degenerate transforms, invalid values, unsupported resizing and poses too large
for the workspace are rejected. The complete box, rather than its pivot alone,
determines the allowed pivot range.

`BirdPlacementRegion.TryPivotBounds(item, box, localRotation, localScale, out bounds)`
evaluates a proposed absolute local pose without temporarily changing the scene.
It transforms the eight configured corners into workspace coordinates, including
child offsets. The normal overload evaluates the current pose. This is a bounds
query for the configured volume, not a physics sweep or dynamic obstacle test.

On a `BirdSnapTarget`, **Match Rotation** uses its authored orientation relative
to the item's parent: compose the hierarchy's local quaternions, then remove the
parent's composed rotation. This keeps orientation independent of reflected or
sheared scale matrices. Objects sharing a parent match their local quaternions;
nested parents work by the same rule. Unity world-rotation getters can disagree
with this composition under mirrored/nonuniform scale, so they are not used for
pose matching or the command adapter's workspace axes. **Match Scale** uses its
factor relative to the grabbed item's
authored reference. The destination position continues to represent the item's
pivot. Capture tolerances are degrees and a symmetric scale ratio: 0.08 accepts
ratios between 1/1.08 and 1.08. Position capture and approach guidance use the
existing workspace-local distances.

A dock cannot authorize an otherwise forbidden rotation or resize. A fixed-size
target can match its known reference factor, but cannot be resized by snapping.
An object whose scale is no longer proportional to its reference cannot claim a
uniform-factor match. With both matching options off, release retains the current
displayed orientation and size.

## Requests and transaction ownership

An input adapter or experience controller calls:

```csharp
bool accepted = grabInteractor.TrySetHeldPose(localOrientation, referenceFactor);
```

The quaternion is normalized after finite/nonzero validation. `false` means no
request was queued, including when no target is held, the transaction is returning,
the target forbids a change, limits are exceeded, or the proposed box cannot fit.
`true` accepts the endpoint request; it does not guarantee that every intermediate
orientation can fit. The Transform changes only when `Process(dt)` runs.

A rejected pose request during a grip freezes pose motion at its current display,
sets `PoseLimited` and blocks placement until a valid request arrives. Translation
can continue. This prevents a previous matching pose from counterfeiting the
user's latest, out-of-bounds intent. Explicit Reset Pose is one way to recover.

`RequestedLocalRotation`, `RequestedScaleFactor` and `CurrentScaleFactor` expose
the current transaction. They reset to identity/1 when it ends. `ResetHeldPose()`
requests the orientation and size captured at acquisition while leaving positional
dragging active. `BirdHeldPoseControls` supplies conventional UnityEvent-friendly
commands: `RotateAroundWorkspaceUp(float degrees)`, `ResizeBy(float multiplier)`
and `ResetPose()`. These are trusted host commands, not separate hand ownership or
network authority. The grab controller retains its single transaction/active hand.

Rotation follows slerp and size follows log-scale with the existing follow rate.
For a held request, weight is `1 - exp(-integral(rate * dt))`. Each evaluation uses
the pose at the start of that request, avoiding accumulated small-angle slerp
approximation. A new request starts from the currently displayed pose. Repeating
the same request does not reset its progress. This preserves continuity, but does
not promise identical output for differently sampled changing gestures.

Every proposed displayed pose recomputes the feasible pivot bounds. Translation
then follows the existing bounded/guided path within those bounds. If a displayed
intermediate box cannot fit, the pose request stops at the last valid sampled pose
and `PoseLimited` becomes true, also blocking a release from being committed.
A new accepted command clears that indication.
This checks displayed samples; it does not certify swept clearance between frames.
Do not drive the owned Transform concurrently with another animation or controller.
External pose, shape, workspace or parent-frame changes cancel the transaction.

## Firm placement and cancellation

Position guidance does not automatically rotate or resize the object. A candidate
can remain visible while orientation/size is wrong. Release requires:

- Existing raw-position intent, visual position proximity and experience-rule
  eligibility, including the rule's atomic commit.
- Both the requested and displayed orientation/size within any enabled capture
  tolerances. A lagging, apparently aligned object cannot hide contrary intent.
- The exact final pose fitting wholly inside the region and respecting the
  target's rotation/size permissions.

A successful release commits exact position plus any required orientation/size,
then raises the existing Placed event once. Invalid release, tracking loss or
explicit cancellation returns position, permitted orientation and permitted size
to the acquisition pose. The object remains reserved during that return. Disable
rolls back immediately. Existing translated-only objects retain their behavior.

Return interpolation also checks the whole box and clamps the pivot. Very narrow
workspaces can have individually valid orientations with no direct fitting
intermediate orientation. If the return pose cannot fit, rollback restores the
committed pose immediately. This fallback is deliberately visible rather than
placing an oversized intermediate box outside the region; it is not a claim of
universally smooth path planning. Author generous workspaces for smooth turning.
Concurrent external geometry/workspace edits can invalidate the original placement;
the controller does not restore those external edits or solve their occupancy.

## Sample and evidence, 2026-09-26

Import **Hanoi Preview**, including Resources, and attach `BirdPosePreview` in a
separate empty scene. It supplies a camera by default. Mouse/wheel aim and set
reach; hold left mouse to move, Q/E rotate 15 degrees, minus/plus resize, R requests
the acquired orientation/size, and right mouse/Escape cancel. The ordinary
`BirdHanoiPreview` continues to use translation-only pieces. Both samples can
accept externally owned logical pointers and omit their camera/desktop producer.

The near station uses 0.3 m per workspace unit; the far station uses 75 m, centered
420 m forward. Its initial 69 m box grows to 86.25 m at the second dock. The full
held building remains beyond 300 m when the logical point returns to the viewer.
The fixed viewing terrace and workspace are authored separately. This is scale
reference and interaction testing, not final Bird World art or layout.

Run `tests/Invoke-UnityPoseChecks.ps1 -UnityEditor <2022.3.22f1 Unity.exe>
-ProjectPath <new-or-marked project> -BuildPlayer`. Actual Unity/D3D11 evidence:

- 5989 assertions cover mutation-free hypothetical bounds versus independently
  transformed corners, both pose-intent gates, exact placement and stable reference
  scale across pickups, every displayed/return corner, narrow-volume stopping and
  rollback fallback, rigid/upside-down/mirrored/nonuniform frames, invalid settings,
  loss/disable/pause, changed parent/policy and remapped persistent prefab commands.
  Most assertions are individual corner checks along trajectories, not distinct
  scenarios. Four rendered views show both scales.
- The Windows x64 Mono player passes normal Update/LateUpdate commands, pose-gated
  placement, the distant full-volume boundary, tracking-loss full-pose rollback,
  two captures and generated-hierarchy cleanup. No explicit manual Process calls
  substitute for its normal frame path. Editor code is excluded from the player.
- The existing Hanoi suite still passes 1216 assertions, six editor captures and
  its Windows normal-frame player/build/capture checks with these runtime changes.
- [Synthetic rate measurements](measurements/manipulation-pose-rates.csv) compare
  a 90-degree/1.8x held request for 0.5 seconds at rate 10. 30/72/120 Hz all produce
  89.3935852 degrees and factor 1.79288518, agreeing with the analytic response.
  The CSV also records the small drift of repeated incremental Unity slerp that
  motivated the request-anchor implementation. This is not measured device pacing.

Pose permissions and dock fields use standard serialization and prefab overrides.
No new dependency or custom Inspector is required. The pose runner preserves the
sample's script GUID and includes its explicit material/shader assets. Generated
projects, images, players and logs live in ignored heavy-repository Validation.

Next integrate an intentional Bird gesture/interaction mode. Actual live-hand
feel, mixed changing-input/render
cadences, moving layouts, physics collision/lift policy, dynamic player exclusion,
network reservations and VRChat-client/headset validation remain separate work.

## Local Udon authoring

`BirdObjectTarget` exposes `allowRotation`, `allowScaling`, `poseReferenceScale`
and factor limits. Assign the reference in the Inspector after sizing the object,
or send `CapturePoseReference` between transactions. `BirdObjectSnapTarget`
exposes the same optional matching flags and tolerances. Defaults keep existing
Hanoi pieces translation-only. Existing policy queries, notifications, menu
arbitration and protected viewing-area checks remain part of the transaction.

`BirdObjectGrip.TrySetHeldPose` accepts an absolute local quaternion and reference
factor from another Udon program. For an event-oriented bridge, set
`poseRequestRotation` and `poseRequestFactor`, send `RequestPose`, and read
`poseRequestAccepted`. `ResetHeldPose` restores the acquisition request. The grip
owns the displayed Transform; do not add a competing rotation or scale writer.
These are local host commands, not network-authoritative requests.

`BirdObjectRegion.TryPoseBounds` writes its `pivotBounds` output on success.
Always check the boolean result. It performs the same mutation-free corner query;
the implementation explicitly assigns a new Bounds because exposed struct
instance mutations are unreliable in Udon. Cross-program calls use output fields
instead of C# out parameters. Private same-program helpers retain out arguments.

`BirdObjectPoseControls` is optional. Its named events `RotateLeft`, `RotateRight`,
`Grow`, `Shrink` and `ResetPose` can be connected to experience-owned controls.
The configured defaults turn 15 degrees about workspace up and resize by 1.25
or its reciprocal. `desktopInput` is off by default; the example enables it for
Q/E, minus/plus and R. It does not read keyboard commands for a VR player. An
optional Text reference reports held/limited/ready/returning state. This adapter
does not define a hand gesture or override the active grip pointer.

The heavy repository's `BirdWorld/Assets/BirdWorld/Scenes/BirdPoseDemo.unity`
adds paired pose stations to a copy of the current map/menu/Hanoi scene. All
eight movable objects register with one grip; the new distant region also joins
the viewing-area gate. The near pose workspace is at (2, 0.78, 2.4), 0.3 m/unit;
the far one is at (400, -12, 500), 75 m/unit. Building height is 69 m initially,
86.25 m at the second dock. Green feedback indicates a permitted drop; the wire
outline shows the required orientation/size. Windows sit clear of the facade.
The previous focused scenes are retained.

Run `tests/Invoke-UnityUdonPoseChecks.ps1 -UnityEditor <Unity.exe>
-ProjectPath <BirdWorld> -Regressions -BuildWorld`. It restores source/meta pairs
and shader assets, compiles actual Udon, drives backing VMs in ClientSim and
captures rendered views. `-Regressions` runs the existing Hanoi and map suites
against the new combined scene. `-Generate` only creates a missing scene; it
refuses to overwrite an authored scene. `-RefineLayout` is an explicit development
helper for label placement, never an automatic validation step. `-BuildWorld` uses the SDK build-only
API and independently checks the Windows artifact's scene catalog. A readable
catalog does not establish scene instantiation or VRChat-client behavior.


## Udon checkpoint and mirrored-dock correction, 2026-09-27 UTC

The compiled-Udon pose fixture passes **13332 assertions**, most of them eight
individual box-corner checks at each trajectory sample. Four rendered views cover
the normal-frame tabletop dock, a rotated/resized distant building held at the
near limit, restoration and the final scene. Both scales commit exact poses and
retain the authored size on re-grip. Invalid requests, raw-versus-displayed intent,
permissions, narrow-volume stopping/return fallback, loss/pause/disable, menu
consumption, changed policy/frame, cross-program commands, offset child boxes and
mirrored/nonuniform frames are covered. Runtime checks use backing VMs, including
normal Update/LateUpdate sequences, rather than calling the C# proxies.

This exposed an ordinary/Udon shared edge case: a matching 90-degree local pose
under a mirrored nonuniform parent could be misread as -90 degrees by inverse
parent-world rotation. Both implementations now compose local authored
quaternions. Ordinary Unity now passes **6015 pose assertions**, including new
mirrored same-parent and independent-parent command/dock cases, plus its Windows
x64 Mono build and normal-frame player/capture/cleanup checks. The preceding
ordinary evidence above is the historical 5989-assertion checkpoint.

The new combined Udon scene also passes the existing **877 Hanoi** and **61 map**
assertions, preserving both seven-move puzzles, menu focus/arbitration, reset,
protected viewing-area behavior, zoom and back-surface flick/coast. The existing
focused UI scene passes **441 assertions** with the new grip runtime.
[Compiled pose timing measurements](measurements/udon-manipulation-pose-rates.csv)
match the ordinary response at printed precision: 30/72/120 Hz produce
89.3935852 degrees and reference factor 1.79288518 after a half-second request.
This is synthetic timing, not device performance or changing-input equivalence.

The new Udon pose scene still uses explicit local desktop input. Actual Bird
hand-gesture pose commands, VRChat client/mobile execution, multiplayer ownership,
physics/lift policy and subjective feel remain open. Quest v0.9 is unchanged.


The final Windows SDK build-only artifact is 305832 bytes, SHA256
`6F7A1674D3D46E7AF9D78A9F4E8AB0266CAB6C56D0831DD9084EE4BD658A7FB0`.
Independent Unity loading reads its expected BirdPoseDemoBuildValidation scene
catalog. The SDK still prints the unexplained internal `Build Finished, Result:
Failure.` line despite API completion and a fresh readable artifact. Catalog
loading is not scene instantiation or VRChat-client runtime validation. No Android
build, upload, account, client launch or headset operation occurred this cycle.
