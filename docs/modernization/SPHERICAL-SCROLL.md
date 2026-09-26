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

The scene uses the **far-side** sphere intersection: the reverse cast hits the
back surface and the logical Bird must extend beyond it. This depth threshold
is part of the recovered scroll behavior. It can allow reaching through to spin
and withdrawing toward a color to stop driving rotation. That affordance is an
inference from the source/scene; the user explicitly confirms the overall
spherical-scroll feel, not an isolated test of this threshold.

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
surface range thresholds; release/coast/stationary reacquisition; rapid reversals;
o angular jump on hand transfer or tracking recovery; offset/scaled pivots;
30/72/120 Hz trajectories; and selection without accidental scroll activation.
Use actual rendered motion plus later physical assessment. Dana's endorsement
of the legacy interaction does not validate a new implementation automatically.

This is a documented behavior target. No new scrolling runtime is active in
the installed Quest v0.8 application yet.
