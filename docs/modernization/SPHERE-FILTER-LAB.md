# Optional sphere-vector filtering in the VRChat lab

Lab 08 adds **SPHERE** after ADAPTIVE on the native Point control. RAW remains
the saved default, followed by original FILTERED, ADAPTIVE, SPHERE, then RAW.
The geometry overlay, optional reach station, calibration, disabled avatar
clicks and original range polynomial are preserved. This is a build-only
comparison; it does not imply a new headset deployment or accepted feel.

`BirdSphereSpaceFilter` is a separate caller-stepped Udon component. It consumes
the existing sphere-derived center-minus-hand vector before polynomial range
expansion. It does not infer another measure of hand opening, use joint-curl
percentages, or adopt the study's illustrative ninth-power range curve.
The previous singular-fit/fist limit policy still supplies `rangeInput`; this
filter does not replace or validate that existing policy.

## Filtering and geometry

Filter direction on a great circle and length separately, with **one common
gain**. At fixed gain, an infinitesimal radial or tangential displacement has
the same first-order response. This addresses the independent-cutoff study's
nearby anisotropy without introducing a working-volume threshold. Finite large
turns follow an arc and preserve range; the method is not a globally Cartesian
filter or a proof of identical legacy near-hand feel.

For unit direction `n`, raw direction `m` and gain `alpha`, the update is
`Exp_n(alpha * Log_n(m))` on the unit sphere S2. `Log_n(m)` is a tangent vector;
its norm is the intrinsic great-circle distance. No yaw/pitch/roll, azimuth or
elevation is stored or averaged. A direction has no meaningful roll, so S2 is
the appropriate space rather than adding an SO(3) orientation state.

The common cutoff is:

```text
minimumCutoff + max(radialSpeedCoefficient * abs(filtered radial velocity),
                    angularSpeedCoefficient * filtered angular speed)
```

Radial velocity uses incoming sphere-center distances in the normalized input
units already used by the range function. Angular speed uses incoming
hand-relative directions in radians/second. Both derivatives have a first-order
low-pass filter. Output gain is `1 / (1 + 1 / (2*pi*cutoff*dt))`.
Illustrative defaults are 4 Hz minimum, radial coefficient 100, angular
coefficient 4 and derivative cutoff 5 Hz. All are authorable; none is a new
pose or openness signal. The speed-adaptive principle is informed by the
authors' [1 Euro filter description](https://gery.casiez.net/1euro/); code is
independently written, and this coupled spherical policy is not claimed to be
their standard Cartesian implementation.

Unlike the initial numerical study, this candidate filters directions in the
world orientation of the hand-relative vector. It does not transport history
with the latest palm rotation: doing so would pass that rotation's noise straight
through. Wrist-driven and finger-driven turns therefore both get the angular
response. The current hand root translates the resulting point directly;
hand-root translation noise remains unfiltered, and moving-origin physical feel
still needs assessment. No torso, head or world-up reference is used.

An exact 180-degree turn has no unique shortest arc. The policy chooses a tangent
by projecting the current hand normal, falling back to the hand-forward axis.
It rejects an unusable pair rather than inventing a world-axis preference.
This is an explicit convention at a geometric ambiguity, not a claim of a
globally continuous antipodal choice. Near coincidence, Log uses its analytic
tangent limit; Exp uses cosine/sinc Taylor limits. Explicit division retains tiny nonzero inputs
below Unity's `Vector3.normalized` cutoff. Exact zero clears history and returns
to the current hand; the next nonzero sample establishes a new direction.

For any fixed rigid transform `(R,t)`, the sphere-center/root difference becomes
`R*(center-root)`. Its length and intrinsic angular speed are unchanged; its
tangent vectors, hand-derived ambiguity axes and filtered direction rotate by
`R`. The scalar gain and range mapping therefore remain unchanged, and adding
the transformed root reconstructs `R*Bird+t`. This is SE(3) equivariance in exact
arithmetic, including the hand-defined antipodal convention. Floating-point
implementations are checked within tolerance, not promised bitwise identical.

## Ownership and lifecycle

Use one policy per cursor. `BirdCursorState.sphereFilter` opts in only while
`smoothing` is true; assigning both optional policies is rejected. Null leaves
the original path intact. The policy returns the filtered pre-polynomial vector;
the cursor applies its unchanged polynomial and keeps the original `rawPosition`
and `rangeInput` available for diagnostics. Presentation remains downstream.

Callers supply a finite sample interval in (0,.25] seconds, the sphere vector,
and finite hand-derived normal/forward axes. Axes need only determine a usable
tangent at an antipode. The avatar path already supplies the canonical palm
axes through its existing hand-limit geometry. A generic caller that omits
those axes can be rejected on an otherwise ambiguous reversal.

Invalid input/settings/time, explicit Cancel and disable invalidate history.
Mode, binding, range calibration and parameter changes seed fresh. The avatar
producer cancels after gaps or source/calibration discontinuities. Policy and
cursor history revisions propagate through `BirdAvatarUiInput`, so switching
filters, including a mode round-trip within one observed frame, cannot become
a false contact sweep. Full-fist return retains the exact hand endpoint.

## Reproduction and acceptance scope

Run `tests/Invoke-UnityTrackingLab.ps1 -Check -CheckBird` with the maintained
Unity editor and BirdWorld project paths. Select Windows, Android or Both.
The normal SDK export is build-only unless `-Launch` is explicitly supplied.
`-AddSphereFilter` is a one-time authoring operation that refuses to overwrite
existing policies; do not use it on the committed Lab 08. Runtime source/meta
restoration includes the new dependency. The standalone-host source restoration
list also includes that dependency, without enabling the policy or rebuilding
the installed standalone app.

`UnitySphereFilterLabChecks` exercises actual compiled Udon: 30/72/120 Hz turns
across near-to-extreme inputs, angular/radial synthetic noise, nearby local
response, mirrored/rotated paths, tiny inputs, origin and antipodes, ordinary
return, independent hands and invalid/lifecycle cases. Normal post-IK frames
exercise the saved avatar bindings, wrist turns, fist, gap recovery, policy
conflicts, mode control and UI contact cancellation. Existing avatar, original
filter and adaptive-policy regressions run alongside it.

## Measurements

Both editor targets pass 106,641 compiled-Udon assertions over 499 normal frames,
plus the saved-scene marker/cadence/render suite. These totals include existing
avatar, adaptive and UI regressions. The per-platform sphere measurements are
identical; the maintained aggregate is
[udon-sphere-space-filter.csv](measurements/udon-sphere-space-filter.csv).

At 72 Hz, 90/170/180-degree steps reach 90% in one sample (13.9 ms), from 10
micrometres to 2 m of pre-polynomial input. A 1-degree adjustment takes seven
samples (97.2 ms), so small-motion lag still warrants physical tuning. The
maximum tested range change during a pure turn is under 1.5 parts per million
across 30/72/120 Hz. The first ordinary return from a 2 m sphere-vector input
to a 2 cm input displays 5.4-7.8 cm of logical reach at those rates, without
requiring a full fist.

With synthetic independent uniform noise of 0.2 mm radial standard deviation
and 0.05 degrees angular standard deviation, the 72 Hz results are:

| Sphere-center distance | Raw radial RMS | Filtered radial RMS |
| --- | ---: | ---: |
| 2 cm | 0.662 mm | 0.274 mm |
| 7 cm | 53.4 mm | 22.1 mm |
| 12 cm | 0.771 m | 0.319 m |

Angular RMS falls from 0.0482 to 0.0198 degrees for that fixture. The remaining
radial noise at distance is substantial: this does not solve the range law's
sensitivity, nor prove a better noise/lag tradeoff than every legacy setting.
The ninth-power curve from the earlier study is deliberately not combined here.
Rigid/mirrored trajectory errors stay below 0.5 parts per million relative to
the tested scale. Additional symmetry checks use 32 rigid transforms and 144
samples each: uniform SO(3) rotations, explicit up/down pointing, pole crossings,
half-turns, mixed sample rates, moving roots, near/far ranges and translations
up to roughly a kilometre. The maximum sphere-vector relative error is
`5.12e-7`, reconstructed world-point relative error `2.01e-6`, and gain error
`1.79e-7`. World error is normalized by the maximum of one metre, logical reach
and transformed root magnitude; near points with modest translations differ
by at most 5.9 micrometres. A separate eight-transform, 96-sample test includes
floating-point subtraction of absolute sphere centers and roots; its maximum
working-volume point error is 34.4 micrometres. These are finite sampled checks
of the mathematical symmetry, not proof of exact machine equality for every
possible transform. Local first-order checks, exact origin return, valid tiny
inputs and lifecycle rejection/recovery also pass.

The [reference range audit](RANGE-REFERENCE-AUDIT.md) confirms the exact original
polynomial and separates it from avatar normalization and the existing limit
blend. Future range tuning should begin with that family and measured input
geometry; this optional filter does not change its constants.

Final platform/build results are recorded in CHECKPOINT.
Do not equate synthetic noise attenuation with physical tracking quality,
successful SDK export with an in-client load, or these tests with a headset
performance measurement. Keep the comparison optional until physical feedback.
