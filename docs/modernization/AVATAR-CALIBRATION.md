# Avatar geometry and range measurements

Measured 2026-09-24 in Unity 2022.3.22f1, Worlds SDK 3.10.5 ClientSim, default desktop avatar. These are actual SDK bone positions and compiled-Udon fits; they are not recorded physical hand tracking. The experimental adapter remains outside the authored demo and disables clicking.

## What the measurement isolates

The 12 fit points are bone origins, omitting four fingertip endpoints from ordinary Bird. The hand-span metric is middle proximal to middle distal, not wrist-to-tip. Eye height was changed through VRCPlayerApi, then restored. The input pose shape is the simulator default, not an intentional open/closed-hand sequence.

| Relative size | Eye height (m) | Right-hand span (m) | Sphere radius (m) | Center-to-root (m) | Raw cursor range (m) |
| --- | --- | --- | --- | --- | --- |
| 0.5 | 0.95 | 0.05076 | 0.09145 | 0.09447 | 20.04 |
| 1.0 | 1.90 | 0.10152 | 0.18290 | 0.18894 | 1250.08 |
| 1.5 | 2.85 | 0.15227 | 0.27435 | 0.28341 | 14220.97 |

The left hand is nearly symmetric: raw ranges are 20.03, 1249.51 and 14214.44 m. Restoring baseline reproduces the original values. Full precision and both hands are in [the measurement CSV](data/clientsim-avatar-scale-2026-09-24.csv).

## Why size matters

The unchanged range law is `F(d) = d + d*d/0.02 + 0.02*(d/0.03)^6`, where d is sphere-center distance from hand root. Scaling all geometry by s scales d linearly, but its dominant sixth-power contribution scales as s^6. The measured range follows this equation; it is not an arbitrary jump added by the adapter.

Center distance divided by hand span stays about 1.861 across tested scales. Radial RMS sphere-fit error divided by span stays about 0.05174 (roughly 5.25 mm at baseline). Neither a finite fit nor a small residual establishes useful cursor control or tracking confidence.

The diagnostic normalization `d_normalized = d * baseline_span / current_span` holds the range near the same 1250 m at every scale. It removes this uniform size dependence but does not calibrate the baseline pose. No production normalization or gain was selected from this single default-avatar pose.

## Implementation consequences

- Preserve the core range law and the large-range interaction ambition. Do not silently replace it with a ray pointer or permanent range clamp.
- The adapter-only 3 m rejection guard hides uncalibrated results; it is not calibration and is not applied to the synthetic/general cursor.
- Next explore an explicit preview calibration action that anchors a chosen neutral pose and separates relative hand-size compensation from desired working range. Test it against more than one pose, scaling, loss/recovery and avatar replacement before making it an automatic mode.
- Missing fingertip endpoints and avatar IK remain separate fidelity problems. Index distal must not silently become a fingertip; clicks stay disabled.

Reproduction: restore integration sources and the UnityAvatarInputChecks helper as described in the integration README, then run `UnityAvatarInputChecks.RunScaleCalibration` in batchmode/nographics. The check compares compiled-Udon fits with the original centered 4x4 equations (0.2 mm tolerance), checks geometry scales proportionally (absolute ratio tolerance 0.002), and checks raw/normalized range agreement (1%). Its CSV is generated in BirdWorld and ignored there; this document preserves the reviewed snapshot. No authored scene or global ClientSim preferences are changed.


## Neutral preview prototype, 2026-09-25 01:11 UTC

The experimental adapter now offers explicit CalibrateNeutral/ResetCalibration events. Calibration records center distance divided by the sum of the two middle-finger bone-segment lengths and inverts the original monotonic range law to anchor this chosen pose at 0.3 m by default. Summed segment lengths replace the earlier straight-line diagnostic span for live compensation because finger bending changes a chord even when bone lengths stay fixed. Each sample adjusts the range-law input for current hand size; the ordinary cursor multiplier defaults to 1.

Compiled-Udon tests with both default-avatar hands passed at 1x/0.5x/1.5x size: raw target range remains within 2 mm of 0.3 m. Filtering can lag after root movement; this tolerance describes the raw target, not every filtered transitional position. Missing data clears calibration; invalid target, explicit recalibration and reset were checked. The local-avatar-change callback compiles but actual swap delivery is not validated.

This is one simulator pose and an opt-in preview policy, not a completed hand-control calibration. Multiple physical poses, hand sizes, comfort, calibration-loss ergonomics and avatar replacement remain gates before it is enabled in the authored world. No inferred fingertip or click input was added.


## Controlled articulation, 2026-09-25 03:12 UTC

With the simulator Animator temporarily disabled, the fixture rotated middle/ring/little proximal finger joints on both hands by +15 and -15 degrees around each joint's local Z axis, then restored them. This is a controlled perturbation, not a validated human open/close pose. SDK middle-distal positions moved about 26.5 mm while middle-finger segment sums stayed constant within 0.2 mm.

| Fixture pose | Right raw target (m) | Left raw target (m) | Result |
| --- | --- | --- | --- |
| Neutral | 0.3000002 | 0.3000002 | Accepted |
| +15 local Z | 0.5791616 | 0.5791630 | Accepted |
| -15 local Z | 0.1498147 | 0.1498139 | Accepted |
| Restored | 0.3000002 | 0.3000002 | Accepted |

All samples retained calibration and kept clicks disabled. Marker visibility matched validity. The fixture restored joint rotations and the Animator's original enabled state without saving SDK/scene edits. [Full measurements](data/clientsim-avatar-pose-2026-09-25.csv) preserve both hands and fit/range-guard flags. This establishes response to these simulator articulations and reversible neutral recovery, not anatomical gesture fidelity, tracking quality, transient filtered response or comfort. The first helper import logged transient missing-helper C# errors before Unity rebuilt successfully and ran the passing test.

Reproduce with both UnityAvatarInputChecks.cs and UnityAvatarPoseFixture.cs in the ignored generated runtime folder, then run UnityAvatarInputChecks.RunPoseVariation. The same fixture is now a compile-time dependency of the other avatar-check entry points.


## Separate diagnostic scene, 2026-09-25 05:13 UTC

BirdAvatarPreview.unity now exposes conventional calibrate/reset controls around this experimental adapter, separate from the synthetic demo. Inputs require calibration, so resetting keeps markers hidden through subsequent samples. The scene labels the approximation and disabled clicks. Its purpose is diagnostic inspection; actual pointer activation, avatar swaps, physical tracking and comfort remain validation gates. See the project README and UnityAvatarSceneChecks for source restoration and reproduction.
