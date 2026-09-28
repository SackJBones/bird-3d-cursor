# Sphere-center Kalman stage (Lab 14)

Dana's Lab 13 feedback: nearby movement feels right, but distant pointing is
too noisy, particularly as the fingers approach a plane. Angular response is
fast enough; increasing the final point's lag alone would give up that benefit.
Dana proposes smoothing the sphere center first and prefers the original
Vector3 Kalman family. This is a bounded implementation of that proposal.

## Order and preserved behavior

The lab now runs:

1. Automatic avatar endpoints and the unchanged sphere fit.
2. An optional `BirdSphereCenterFilter`, before geometric hand limits and range.
3. Existing palm origin, sphere-directed aim, behind-palm correction, fist and
   singular continuation, then the unchanged original range polynomial.
4. The existing ADAPTIVE Vector3 Kalman output policy and presentation.

The new filter never overwrites the actual fitted center/radius/confidence.
The gold geometry remains the raw fit; cyan/pink show the resulting Bird.
`rawPosition` and `rangeInput` retain the original unfiltered diagnostics;
`filteredSphereCenter` exposes the center used by the added stage. Clicking
remains disabled in this avatar experiment, and automatic startup needs no SET.

The extra stage has zero influence through 4 m of incoming, unfiltered Bird
range. Its influence follows the existing smooth logarithmic 4–20 m transition,
reaching full effect at 20 m. Thus near-only histories have exactly the old
output, rather than adding a second delay to the already-liked working volume.
These are authorable range settings, not another measure of hand opening.
Influence never depends on the lagging displayed point. Ordinary near return
immediately seeds current geometry; the existing exact fist return is retained.

## Vector state and covariance

The state is `(fittedCenter - currentPalmOrigin) * rangeDistanceMultiplier`:
a Vector3 in world axes, using the same avatar-normalized units as the range
function. Palm translation is current, not smoothed; history is not transported
by current wrist rotation. This avoids converting wrist rotation noise directly
into instantaneous output rotation. No torso, head, Euler angles, or preferred
world axis participates.

The recurrence is the original `KalmanFilterVector3` scalar-covariance update:

```text
predicted = P + Q
K = predicted / (predicted + R)
P = R * predicted / (predicted + R)
centerState = (1-K) * centerState + K * sample
```

The initial sample seeds state exactly. At 72 Hz, Q is 1e-6 and R is 9e-6 in
normalized square metres. They are explicit starting assumptions, not measured
Quest tracking covariances. Q scales with elapsed time and R inversely with
elapsed time to keep the response comparable across frame rates. This is still
an isotropic Vector3 Kalman recurrence; it does not maintain angle coordinates
or use the earlier S2/1-Euro experiment.

Scalar gain and covariance are rotation/reflection invariant. A fixed rigid
transformation rotates the center-minus-palm vector; averaging commutes with
that rotation, and adding the transformed current palm restores the same rigid
transformation of the result. Tests exercise the complete downstream chain as
well, within floating-point tolerances.

## Limits and lifecycle

Cartesian averaging cuts the chord during a sudden large turn. It can briefly
shorten the center vector, which the polynomial magnifies into a visible range
contraction. The measurements explicitly include this tradeoff; it is not the
radius-preserving intrinsic filter explored earlier. Nearby motion bypasses the
extra stage, but the far-field noise/latency tradeoff still needs physical use.

A singular fit has no center to smooth. Its existing continuation remains in
charge, and the center filter clears once on fit loss. The fallback's independent
range variation is not magically removed by filtering the valid fitted center.
No range clamp or new openness signal is introduced.

Tracking loss, disabled source, calibration/axis changes, policy changes and
invalid settings clear history. Center-policy revisions also clear downstream
filter/contact histories; changing a setting cannot invent a UI sweep. Each
hand owns its own component and state.

## Reproduction

Use the maintained Unity editor and restored BirdWorld project with
`tests/Invoke-UnityTrackingLab.ps1 -AddCenterKalman -Check -CheckBird` once to
upgrade Lab 13. The authoring operation refuses to duplicate existing center
filters. Subsequent `-Platform Both -Check -CheckBird` validates and exports the
saved Lab 14 scene through the normal SDK. `-Launch` is an explicit separate
single-platform deployment option. The standalone app is unchanged.

`UnityCenterKalmanLabChecks` compares compiled Udon against the actual original
Kalman class, exercises near identity, both hands, rigid/reflected transforms,
30/72/120 Hz response and noise, far return, invalid input, automatic post-IK
startup, retained raw geometry, and interaction-history cancellation. Bone-space
noise cases additionally traverse the saved avatar adapter and near-planar fit;
they are synthetic evidence, not headset tracking recordings.

See the latest CHECKPOINT for measured results and the exact build/deployment
state. Physical acceptance of the new far behavior must remain separate from
compilation, numerical checks and successful SDK export.

## Initial measurements

The [Windows compiled-Udon measurements](measurements/udon-center-kalman-windows.csv)
include the unchanged downstream ADAPTIVE policy in both comparison paths.
The [Android editor-target measurements](measurements/udon-center-kalman-android.csv)
have the same controlled filter results; normal-frame bone-noise results vary
slightly with editor cadence. Both targets pass 146,378 compiled-Udon assertions
over 1,768 normal frames, including the earlier independent regressions.
At 72 Hz, synthetic independent center noise gives 45–56% lower distant RMS
error. A 90-degree direction step reaches 90% of the new angle in 125 ms,
versus 111 ms without this stage. In the partly blended mid-range (about 7.6 m),
the improvement is 12%, with 306 ms versus 292 ms to the same turning criterion.
The broader 30/72/120 Hz set shows that the middle transition benefits less than
the fully active far field; the 120 Hz partial-blend case improves only 4%.

The large-turn tradeoff is significant: far logical range briefly falls to
roughly 12–15% of its target in that abrupt 90-degree step. This is caused by
averaging center vectors before the sixth-power term, combined with the existing
output return protection. It must be assessed physically; fast angular response
alone does not establish good feel. No radius-preserving correction is hidden
inside the requested Vector3 filter.

In a separate normal-frame editor test, independently perturbed finger origins
(up to 0.3 mm per axis) pass through the actual saved avatar adapter and fitter.
The Windows results reduce RMS by about 47% at the 89 m fixture, 29% at 185 m,
and 16% in the nearly flat 425 km fixture. The last fixture has no valid sphere
in 69 of 144 samples, illustrating the remaining fallback boundary. These are
controlled poses, not recordings of Dana's hands, and their editor sample
cadence is not a claimed device frame rate.

The direct Kalman-class comparison differs by at most 1.71e-8 normalized metres;
the complete chain's rigid/reflection check reaches 2.71e-6 relative error.
Near-only output is bit-identical to the prior path, and ordinary near return
discards prefilter history immediately. Full-fist return remains exact.
