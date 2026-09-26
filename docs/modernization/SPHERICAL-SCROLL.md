# Bird spherical scrolling and the color selector

Dana confirmed on 2026-09-26 that the recovered RotateWithBird interaction felt
particularly successful. Treat its behavior as a strong physical reference for
the maintained UI. The selector presents twelve colored particle fireballs on a
dodecahedron. Point through its surrounding sphere, flick to turn the choices,
and let momentum carry the rotation until the desired color is accessible.
Preserve this interaction flavor using conventional Unity components and tooling.

## Verified recovered behavior

Reference source is byte-preserved in
`Reference/RecoveredBirdUI2024/fireball-may-july2024/Assets/Scripts/`:

- `RotateWithBird.cs` acquires a Bird when its ray intersects the assigned
  collider and the logical Bird range exceeds the hit distance. No click/hold
  is required to scroll. Motion between consecutive center-relative hit
  directions produces a cross-product angular-velocity estimate, smoothed
  toward the current input. Rotation continues after the ray disengages while
  angular velocity decays. The target rotates in world space.
- `DodecahedronGenerator.cs` arranges exactly twelve children at the twelve face
  centers, multiplied by its radius setting. It handles layout and optional
  pentagonal outlines; the scrolling lives in RotateWithBird.
- The recovered SampleScene's Dodecahedron object has both scripts and a
  centered SphereCollider. Serialized settings are collider radius 5, generator
  radius 2.5, `castFromBack=true`, `momentum=.01`, and outlines disabled. These
  are local scene settings, not universal Bird constants or verified final
  world dimensions. It has twelve FireButton prefab children.

Dana explicitly confirms the **larger sphere and far-side activation boundary**
are essential. The sphere surrounds the smaller dodecahedral selector, leaving
space to wave Bird among the twelve colors without driving the selector's
rotation. Entering the front surface, moving among the fireballs, or moving
between the dodecahedron and the far sphere surface must not start scrolling.
The logical Bird point must extend past the sphere's **back** surface before
ray motion drives spherical scrolling. Withdrawing back inside disengages that
drive; existing momentum can still coast and decay. This is now a user-confirmed
design requirement, not merely an inference from the archived code.

The implementation uses the far-side sphere intersection, compared with Bird's
logical range. No click/hold is needed and front-surface intersection alone is
insufficient. Keep the outer interaction sphere independent from the visual
selector size so this free interior working region survives layout changes.

The script's public default momentum is .5, but the actual scene uses .01.
Its damping multiplies velocity by approximately `1 - dt*(1-momentum)`, and
input tracking uses `Lerp(..., dt*10)`. Preserve the scene as the tuning reference;
do not mistake the parameter for a per-frame velocity retention factor.

## Maintained implementation boundary

Make spherical scrolling a reusable interaction component with an assigned
sphere/pivot and rotation target, usable by a color selector or a map. Keep
face layout and particle/color choices separate. Inputs come from Bird's
logical ray/point and tracking state; no render-shell coordinates or edits to
the geometric point. Selection of a color remains a distinct UI action.

Preserve free spherical motion, the reach-through threshold, smooth following,
flick momentum, coasting and reacquisition. Retain conventional Inspector
settings and optional feedback/events. Rework legacy implementation details
where needed: physical damping with defined time units, frame-rate consistency,
finite large-angle handling, collider-center/scale correctness, ownership and
tracking-loss lifecycle. Avoid the arbitrary 1000/2000 m ray limits and a zero
world position as a missing-hit sentinel. Do not silently add a drag-button
requirement or turn this into only horizontal carousel scrolling.

Focused checks should cover slow sweeps and flicks in multiple axes; front/far
surface range thresholds (including no activation at front entry or anywhere
inside the larger sphere, and activation beyond the back); release/coast/stationary reacquisition; rapid reversals;
o angular jump on hand transfer or tracking recovery; offset/scaled pivots;
30/72/120 Hz trajectories; and selection without accidental scroll activation.
Use actual rendered motion plus later physical assessment. Dana's endorsement
of the legacy interaction does not validate a new implementation automatically.

## Maintained components, 2026-09-26

`Bird3D.UI` now contains three independent pieces:

- `BirdSphereContact` computes the far intersection from the finite origin-to-point
  segment. It honors collider center and the maximum absolute world scale, checks
  finite inputs, rejects tangents, and has no 1000/2000 m reversal/cast limit.
  Closest-approach arithmetic uses doubles to avoid the large squared-distance
  subtraction in the naive quadratic formula. World positions still have Unity's
  float precision.
- `BirdSphericalScroll` owns one active pointer, rotates an assigned target around
  the collider's world center and exposes Started/Stopped UnityEvents. A 3 mm
  entry margin avoids repeated initial activation immediately on the back surface;
  crossing back inside disengages immediately. No click level is inspected.
- `BirdDodecahedronLayout` places an explicit array of twelve items along unit
  face normals, with an actual center-to-item radius. It does not rotate, select,
  spawn particles or decide color behavior.

Keep the sphere outside the rotating content hierarchy, so an offset collider
does not change its center when the target rotates. Assign both hands to one scroll
component; the active pointer retains control while eligible. A different pointer
gets a fresh contact baseline and no inherited throw. An optional panel limits
interaction to its owner while Open. Closing/backgrounding that panel, invalid
geometry, source loss/user change/destruction, disable or a pause over 250 ms
cancels angular velocity. Ordinary withdrawal retains inertia. Reacquisition
rebases contact without an orientation jump; stationary input smoothly brakes.
Loss/recovery occurring between two scroll frames also rebases safely.

Input-following rate defaults to 10/s and damping to 0.99/s, corresponding to the
recovered scene's approximate continuous rates. The angular-velocity input is the
axis-angle between successive back-contact normals divided by the update interval.
It uses atan2 rather than asin(sine velocity * dt), remains finite for large
angles, and is limited to 720 degrees/s. Exactly antipodal contacts have no unique
axis and rebase without inventing a spin. The integrated model is:

```
driving: d(angularVelocity)/dt = response * (inputVelocity - angularVelocity)
                                - damping * angularVelocity
coasting: d(angularVelocity)/dt = -damping * angularVelocity
```

The end velocity and integrated angle use the exponential solution within each
interval. Fixed-axis constant-rate input therefore agrees across update rates;
curved multi-axis paths still have sampling/rotation-composition error. This is
an adaptation of the legacy feel, not a claim of identical discrete trajectories.
Submit once before each scroll LateUpdate, or call Process with the matching
sample interval. Repeated revisions do not repeat the last angular displacement.
Mixed input/render cadences have not yet been measured. The producer must cancel
when data stops. On rotation, physics transforms are synchronized so child hit
volumes agree with the displayed content on the next query; device cost remains
to be measured.

## Example and validation

Import Menu Preview and attach `BirdSphericalSelectorPreview` in an empty scene.
It creates twelve independently selectable color orbs on a 30-edge dodecahedron,
inside a larger three-circle sphere guide. Its separate Color Selected UnityEvent
changes a result sphere by default. The particle fireball artwork is still a later
presentation step. `Initialize(inputs)` embeds it without creating a camera or
synthetic input. The Quest v0.9 host binds both accepted port points through this
path, without changing the fit/range/filter/click laws or using marker projection.
The embedded example faces the initial viewer, with dark label backings separated
from the text by 2 cm. A separate Play Mode check renders forward and facing views
against the actual bright Quest vista, and verifies external-input color selection
without scrolling (`UnityQuestUiRenderChecks.Run` in the generated Quest project).
These are real Unity camera renders, not XR stereo or headset perception evidence.

The menu test runner now includes spherical checks and camera captures:

```powershell
./tests/Invoke-UnityMenuChecks.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/Validation/Menu2022'
```

The checks exercise the actual components in Unity Play Mode: front/interior
nonactivation, far thresholds, 1e12 m logical reach, Unity reverse-ray contact
agreement for offset/rotated/nonuniform/negative-scale spheres, coasting, both
hands, transfer/recovery, pause/disable/destruction, reentrant callbacks, panel
focus, layout geometry and interior color selection independent of scroll drive.
Constant-axis and reversing multi-axis trajectories run at 30/72/120 Hz. Generated
CSV measurements, static captures and a timed rendered sequence live under
ignored `Validation/Menu2022/MenuCaptures`; these are synthetic timestamps and
real Unity rendering, not device frame pacing or a physical feel assessment.

Current result: **316 spherical assertions**, in addition to 57 menu assertions.
The fixed-axis drive/coast endpoint is 84.28236 / 84.2822647 / 84.28227 degrees at
120/72/30 Hz, versus 84.28226 degrees analytically. The multi-axis trajectory's
final angular difference from 120 Hz is 0.004876 degrees at 72 Hz and 0.042053
degrees at 30 Hz. These use a small-angle atan2 measurement instead of Unity's
Quaternion.Angle cutoff. See [saved measurements](measurements/spherical-scroll-rates.csv).
The upside-down rigid-frame comparison and offset target orbit also pass.

Four static sphere states and 120 camera-rendered frames cover free interior
movement, drive, withdrawal/coast and stationary reacquisition. Encode the sequence
with `python tests/render_spherical_motion.py <MenuCaptures>` (Pillow required).
The Windows player also passes actual frame-driven LateUpdate selection, scroll,
coast and cancellation checks, with a separate camera capture. Its requested
60 Hz loop is not a measured performance claim.

The package UI is ordinary Unity C#. VRChat uses the separate maintained
`BirdUiSphericalScroll` Udon counterpart and local station in [UI-UDON.md](UI-UDON.md);
the package MonoBehaviours themselves do not execute as world Udon.
Deployment/build outcomes are recorded separately in CHECKPOINT.md and
QUEST-LIVE-HANDS.md. Physical comparison with the endorsed legacy flick feel,
mixed-cadence input, moving layout transforms and multiplayer remain pending.
