# Pose-aware limits after physical feedback

Dana tried the previous continuation and reported a sudden size cap and a
closed fist producing the maximum sphere. Those are failures of that design,
despite the earlier synthetic paraboloid tests passing. Version 0.5 replaces
it with an optional **point law**, downstream of the ordinary sphere fit.
Original `Bird.cs` remains the comparison reference.

Version 0.6 adds a configurable 45-degree tilt toward the knuckles to the flat
law, based only on the palm frame. Dana supplied a usable recording and prefers
plain Inflate. See [recorded replay and the house/vista](QUEST-VISTA.md) for
current direction, evidence and presentation; the v0.5 law below is its baseline.

## Two laws, plus a closed endpoint

`BirdSphereFit` again only fits a sphere. It also reports a continuous
conditioning confidence; it never invents a maximum sphere for an invalid
hand. `BirdCursorState.useHandLimits` defaults off. Opting in requires the
canonical 16 Bird fit points, the original `.6*indexBase + .4*thumbBase` root,
and a verified palm-outward normal. The Quest adapter supplies these. The
unverified VRChat avatar adapter keeps the option off.

The classifier uses signed MCP elevation and PIP/DIP flexion of the middle,
ring and little fingers. Index motion remains reserved for interaction.
Its sign comes from a stable palm frame, including when an MCP passes 90
degrees; backward flare stays in the extended regime. Average fingertip-to-MCP
chord divided by chain length provides an additional compact-fist measure.
These are heuristic pose measures, not a claim of anatomical calibration.

- Ordinary well-conditioned poses use the unmodified sphere-center vector.
  The new flat blend is zero at 45 degrees total bend and above. The closing
  blend is zero through 140 degrees, unless the fingers are already compact.
- Between 45 and 15 degrees the vector blends smoothly toward a separate
  palm-normal law. At 15 degrees and below, an inverted fitted sphere has no
  influence on the point. A smooth conditioning weight also removes an
  underdetermined fit; degeneracy alone no longer means “send it far.”
- The second law's input distance is
  `h / sqrt(bendRadians^2 + (h / maximumLimitDistance)^2)`, where `h` is half
  the mean three-finger chain length. It approaches 2 m with zero slope at a
  flat hand; overextension retains that endpoint. This 2 m is **range input**,
  corresponding to about 1.76 billion m after the original polynomial.
- A separate closing weight shrinks the range-input vector to zero between
  140 and 210 degrees, or as mean chord/chain ratio falls from .55 to .35.
  A fully closed classified fist returns exactly to the weighted hand root.
  It also clears distant Kalman history at that endpoint and releases clicks.

Blending happens before the polynomial, avoiding the enormous displacement
that even a tiny linear weight on a billion-meter output would introduce.
The original polynomial and Q=.001 / R=270*d^3 recurrence remain. Its weighted
sum is evaluated in a form that avoids cancellation on a very long return.
Startup/recovery still seeds at the current measurement, as in earlier ports.
The limits are configurable experiment choices, not a new definition of Bird.

The diagnostic wire sphere displays the actual fit and fades with its
contribution. It does not pretend the alternate point law has a fitted sphere.
The optional cursor size/trail/depth renderer is unchanged.

## Verification and limits

The shared `UnityPalmFitChecks` now uses articulated finger chains instead of
requiring arbitrary point clouds to be classified as hands. It runs both in
actual Unity C# and through the compiled Udon cursor. It covers ordinary
legacy parity at shipped parameters, a 2401-sample close/open/flare trajectory,
folded planar inputs, MCP flexion across 90 degrees, both mirrored hands,
rigid transforms, three hand scales, noise, index independence, invalid data,
recovery and a one-sample billion-meter-to-fist return.

Preserve the original 400-sample fit/range/Kalman comparison, baseline compiled
sphere checks and compiled range/click/filter tests as separate evidence.
Passing synthetic checks does not establish natural feel. No captured human
joint sequence was available for the initial v0.5 revision; thumb opposition, asymmetric
finger curls, tracking jitter and individual closure thresholds still need
physical evidence. The old grid tests did not cover the failure Dana found.

## Low-effort real joint recording

In Bird Live Hands v0.5, dwell an index fingertip on **Record 20s** for 0.6 s.
A three-second countdown lets the hand leave the button. Then slowly go
fist → cup → flat → slight backward flare → fist, or reproduce an awkward
motion. One hand at a time is useful, but both are recorded. The display
reports recording time and the saved filename. Headset pause saves a partial
capture; an empty countdown is canceled.

Files stay on the device, with no network upload, under
`Android/data/org.bird3d.livehands/files/BirdJointTraces/`. Each JSONL row has
schema/app version, relative time, hand, tracking and pose validity, 20 joint
positions (four per finger, thumb through little), palm frame, raw/filtered
points and blend diagnostics. No camera imagery or account data is collected.
The recorder buffers at most 6000 rows; it snapshots data before subsequent
joint mutation. Its editor integration test checks countdown, immutable
snapshots, tracking loss, pause/save and JSONL readback. Device control and
capture usability remain subject to a physical attempt.

When a capture exists, retrieve it with ADB to an ignored local validation
folder. Do not commit a person's raw motion traces automatically. Replay the
inputs independently of recorded outputs, compare legacy/limited raw points
and filters, and inspect transitions before changing thresholds. User recording
is optional; unavailable hardware or recordings must not block other work.
