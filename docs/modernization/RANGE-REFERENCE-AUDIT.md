# Reference polynomial and VRChat input audit

Dana asks whether the original polynomial supplies a better starting point than
inventing another curve, while allowing changes when avatar geometry requires
them. Yes: retain its family as the baseline, and isolate input calibration,
limit handling, range shaping and filtering before attributing changed feel to
the avatar or replacing the polynomial.

## Direct source comparison

The original `Bird.cs` at commit `346e017` (2024-08-23), the maintained package
`Unity/BirdPlugin/Runtime/Scripts/Bird.cs`, and `BirdCursorState` use the same
range polynomial and constants:

`F(s) = s + s^2/0.02 + 0.02*(s/0.03)^6`.

Here `s` is **distance from the weighted hand root to the fitted sphere center**,
not the radius and not a finger-opening percentage. Sphere radius and center
distance are related, but should not silently be substituted for one another.
The root remains 60% index base / 40% thumb base. The canonical 16 fit points
likewise retain the original thumb/intermediate/distal/tip, index base and
middle/ring/little chains. Input correspondence and inferred tips are distinct
from matching that list conceptually.

The reference itself calls out two characteristic scales (2 cm and 3 cm) and
explicitly invites replacing the range function. It already has the desired
qualitative properties: zero at the origin, unit initial slope, monotonic
increase, a modest quadratic near term and a dominant sixth-power far term.
It is not a capped-distance mapping.

| Sphere-center distance | Reference cursor reach |
| --- | ---: |
| 2 cm | 4.18 cm |
| 3 cm | 9.5 cm |
| 4 cm | 23.2 cm |
| 6 cm | 1.52 m |
| 7 cm | 3.54 m |
| 8 cm | 7.59 m |
| 10 cm | 28.0 m |
| 12 cm | 82.8 m |

Thus it supplies the near/architectural/landscape progression, but a small
difference in sphere-center distance around 6-10 cm has a large effect. At 7 cm
its slope is approximately 285 metres of cursor motion per metre of input;
0.2 mm radial input noise is about 57 mm of first-order cursor noise before
filtering. Matching the formula alone cannot guarantee matched feel.

## What changes before or after it in VRChat

1. The adapter consumes avatar finger bones and estimates five endpoints from
   distal rotations and an authored 0.8 distal/preceding segment ratio. It does
   not receive the same raw physical fingertip data as the standalone OpenXR
   host. Different articulation/proportions can change the actual fitted sphere.
2. The normalized, conditioned sphere solver has the same algebraic fitting
   objective as the reference but rejects near-singular cases and reports fit
   confidence; numerical behavior at degeneracy is deliberately different.
3. The existing flat/fist limit law blends a palm-based fallback into the
   center-minus-root vector before the polynomial. Conditioning can also
   increase its influence. This is an intentional departure from unconstrained
   legacy fits, not an effect that can automatically be blamed on the avatar.
4. `BirdAvatarHandInput` multiplies that vector's length by
   `referenceFingerLength / fingerLength`, default reference 9 cm. It also
   adjusts the limit endpoint into those normalized units. Legacy `Bird.cs`
   applies the polynomial directly to physical center distance without that
   avatar normalization.
5. Original FILTERED applies the legacy scalar Kalman recurrence after range
   expansion; the optional SPHERE policy filters the sphere vector first.
   Preserve RAW when isolating geometry/range from temporal behavior.

Normalization deserves particular scrutiny because its factor `k` multiplies
the dominant far term by `k^6`. In the **synthetic articulated avatar fixture**,
mean finger length is 7.26 cm, so normalization to 9 cm gives `k=1.240` and
multiplies that term by about 3.63. Conversely, the ClientSim default avatar's
measured 11.645 cm chains give `k=0.773`, reducing it to about 0.213. These are
fixture examples, not measurements of Dana's current physical hands or avatar.

[reference-range-normalization.csv](measurements/reference-range-normalization.csv)
inverts the unchanged polynomial for existing compiled-Udon fixture observations
and compares the same effective vector with normalization removed. This isolates
the scalar's effect; it does **not** refit the points or recompute the limit law.
Fixture curl labels must not become a new runtime openness signal.

## Configuration direction

The reference family is a good, less disruptive starting point:

`F(s; a,b) = s + s^2/a + a*(s/b)^6`, with legacy defaults `a=.02`, `b=.03`.

Keep hand-size calibration explicit and separate from the near/far response
profile. Changing `b` delays or advances the far term while leaving the two near
terms intact. For example, `b=.04` changes the result at 8 cm from 7.59 m to
1.68 m, while preserving unbounded growth. This is an illustration, not a
selected default or a calibrated mapping to 65/80/90% hand opening.

The earlier ninth-power example remains a design study, not a requirement.
Before introducing it, compare the original family with sensible characteristic
scales, inspect actual sphere/limit/normalization values, and keep the user's
comfortable working volume as the primary target. Configurability should use
sphere geometry and clear distance scales, never an added openness classifier.
No production range constants were changed by this audit.

Reproduce the numeric tables with the standard-library-only script:

```text
python tests/study_reference_range.py --out <results> --avatar-csv <avatar-hand-input.csv>
```

The optional CSV is the existing synthetic compiled-Udon lab output under ignored
Validation/TrackingLab. The retained curve table is
[reference-range-curve.csv](measurements/reference-range-curve.csv).
