# Hanoi Preview

Import this sample through Package Manager, including `Resources`. Add
`BirdHanoiPreview` to an empty GameObject in a separate empty scene and enter
Play Mode. It creates a small tabletop puzzle and a distant puzzle made of
building sections, using the same manipulation components at 375 times the scale.

Desktop controls:

- Mouse position aims; the wheel changes the synthetic Bird point's reach.
- Hold the left mouse button through a top piece to pick it up, then move it.
- Follow a visible approach guide toward a legal dock. Release when the dock
  feedback indicates readiness. A larger piece cannot go on a smaller one.
- Right mouse or Escape cancels and returns the piece. R resets both puzzles.

The goal is to move the complete stack from dock 1 to dock 3. Three pieces can
be solved in seven moves. Invalid drops and tracking loss return to the last
committed position without changing the move count. Closing a hand cannot pull
a distant building outside its authored work area toward the viewer.

For a host that supplies accepted Bird samples, assign `externalLeft` and
`externalRight`, turn off `desktopInput`, and optionally turn off `createCamera`
before initialization. Submit to those `BirdPointerInput` components from the
host and cancel them on tracking loss. The preview creates its own small
environment; a production host should author a layout using the underlying
components. Disable/remove the preview to clean up its generated objects and
materials; unrelated children and external inputs are preserved.

Runtime components live in `Bird3D.Manipulation`, outside the solver and editor
assemblies. `BirdHanoiBoard` is a sample placement rule, not a geometric Bird law.
Inspector references and Grabbed/Placed/Cancelled UnityEvents support conventional
authoring. Box volumes must enclose the intended object and start inside their
placement region. Distances are region-local; changing the region's scale scales
the whole interaction. Use one driver for the registered pointers/objects and
configure outside an active transaction.

This is translation and allowed-destination placement, not physical collision
avoidance or an automatically networked puzzle. Pieces can pass through one
another while moving. The host must keep the user's movement area outside a
distant building workspace. A separate local Udon adaptation includes menu/world
arbitration and a viewing-area gate; it does not run these ordinary MonoBehaviours.
VRChat-client and physical hand testing remain separate work.

Measured on Unity 2022.3.22f1 / D3D11: 1216 editor assertions, six camera captures,
and a Windows standalone build/runtime/render pass with synthetic input. See the
repository's `docs/modernization/OBJECT-MANIPULATION.md` for contracts, limitations
and the reproducible test runner. This sample is not installed on Quest by importing it.

## Optional pose docking

Attach `BirdPosePreview` in a separate empty scene to try rotation and size-aware
docks at tabletop and building scale. Hold left mouse to move as above; **Q/E**
rotate 15 degrees, **minus/plus** resize, **R** requests the acquired orientation
and size, and right mouse/Escape cancels the whole placement. The second outline
requires a 90-degree turn and 1.25x size. Position alone cannot qualify the drop.

On your own `BirdGrabTarget`, enable Allow Rotation/Allow Scaling, capture the
authored Pose Reference Scale through its context menu and set factor limits.
That reference persists across placements. A `BirdSnapTarget` can require its
world rotation and an exact reference factor, with explicit capture tolerances.
Leave these options off for translation-only pieces such as Hanoi.

`BirdHeldPoseControls` exposes float-valued UnityEvent commands for rotation and
resizing. A gesture adapter can instead call `BirdGrabInteractor.TrySetHeldPose`
with an absolute local quaternion and reference factor. This requests a bounded
pose; do not write the held Transform concurrently. Rotation/size gestures are
experience choices, separate from Bird's geometric point and press input.

Normal rollback restores position, orientation and size. A narrow workspace with
an impossible return orientation falls back to the committed pose immediately.
These are per-frame volume limits, not swept collision avoidance. See maintained
`docs/modernization/OBJECT-POSE.md` for full ownership, timing and capture contracts.
The separate local Udon pose transaction is documented there; the gesture adapter
below currently belongs to ordinary Unity. The example accepts the same external
input and camera options as the Hanoi preview and cleans up its generated hierarchy.

With both external pointers assigned, `BirdTwoHandPose` adds an optional clutch:
hold an object with one Bird, then point the other through it and make a fresh
press. Turn/spread the tracked hand roots to rotate/resize; release the secondary
to freeze the displayed pose while continuing translation. Input origins must be
hand roots in world meters. A head-based synthetic ray producer is unsuitable for
this gesture. Either hand can be primary. No point/range/filter law changes.

Disable Create Environment to share a host's setting; Tabletop Position and
Building Position configure the two stations. Camera/desktop producers are also
optional. The Quest harness uses these options in its exclusive Objects mode.
See `docs/modernization/TWO-HAND-POSE.md` for limits, loss, ownership and evidence.
