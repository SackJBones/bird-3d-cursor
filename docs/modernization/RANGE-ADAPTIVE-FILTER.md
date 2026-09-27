# Optional range-adaptive filter in the VRChat lab

`BirdRangeAdaptiveFilter` is an experimental caller-stepped Udon component,
separate from the fit, hand-limit law, original polynomial and rendering.
Lab 07 exposes **RAW -> FILTERED -> ADAPTIVE -> RAW** on the existing native
Point control. FILTERED retains the original scalar-covariance recurrence.
The authored default remains RAW so that geometry can be assessed directly.
The deployed Lab 05 is not replaced by these local build-only changes.

## Policy and scope

This combines the raw-range responsiveness floor from MOTION-AND-CONTACT.md
with the stale-history contraction studied in FILTER-CANDIDATES.md. It uses
incoming hand-relative range, not delayed displayed range, to activate. Unlike
the earlier contraction study's maximum flat-pose weight, this candidate keeps
the maximum **raw-range activation weight** since the last return into the
working volume. It therefore also covers distant ordinary fits.

At raw range r, activation is smoothstep(log(max(r,4)/4)/log(20/4)), clamped to
0..1. The original Q=.001 and R=270*d^3 use the same polynomial-input distance d.
Before the Kalman update, predicted covariance has a floor
R*(exp(dt*activation/.05)-1). This bounds gain below by
1-exp(-dt*activation/.05) while leaving stronger original gains intact.
Near-only histories at or below four metres use the original recurrence.
Measured C#/Udon differences stay below one micrometre; see results below.
This is not a claim of identical near behavior after a
distant history: clearing that history is an intended change.

On a return, history outside (raw range + 4 m) is gently contracted. If e is
the excess length, replace it by e/(1+e*dt*72), blending that correction by the
remembered activation weight. The join at zero excess has matching value and
first derivative. Only filter history changes; logical raw reach is unlimited
by this policy. The full-fist endpoint returns exactly to the hand and clears
history. Once shown range is within four metres, remembered activation clears.

This is a bounded pragmatic candidate, not the proposed coordinate warp. It
still interpolates Cartesian chords: during a large angular change the shown
point can temporarily move radially inward. Greater responsiveness also admits
more distant jitter than a very sluggish filter. Q remains sample-based, so
the whole filter is not sample-rate invariant even though the gain floor uses
elapsed time. Moving-origin tests check rigid-frame consistency, not a claim
that hand-root motion is removed from the world-space filter. Physical comfort,
avatar fidelity and click quality still require direct assessment.

## Integration and lifecycle

Use one policy per cursor. Assign `BirdCursorState.adaptiveFilter` and enable
`smoothing` to opt in; null retains the original recurrence. Restore all runtime
source/meta pairs, including the optional class, before compiling. Explicit
producers supply a finite `sampleDeltaTime` in (0,.25] seconds each sample and
call `Cancel` after discontinuities. The avatar producer supplies unscaled
elapsed sample time. Suspension/clock rollback cancels history and seeds the
first resumed sample with a nominal interval, which is not integrated.

Invalid inputs, invalid policy settings, disabled policy, tracking loss and
explicit cancellation invalidate history. Binding changes, bypass/resume and
settings changes seed the current raw point. Cursor and policy history revisions
let the avatar UI bridge cancel contact even when a mode round-trip or reset
happens between two observed frames. A fresh mode/configuration seed must not
look like a deliberate sweep through an interactive object. Avatar clicks
remain disabled. The policy neither owns input sampling nor calls presentation.

## Reproduction

Run `tests/Invoke-UnityTrackingLab.ps1` with UnityEditor, ProjectPath,
`-Check -CheckBird`, and Platform Android, Windows or Both. `-SkipBuild` omits
SDK export; the default export is build-only. `-AddAdaptive` is a one-time
authoring operation which refuses existing policy components. Do not use it
on the already committed lab. `-Launch` is a separate explicit device/client
action and is not part of this checkpoint.

`UnityAvatarFilterLabChecks` exercises actual compiled policies through Udon
events and heap inputs, then tests native mode changes and the normal post-IK
avatar pipeline. It measures near-only parity, 90-degree turns at 1 km and
1 billion metres after short/long histories, ordinary return, angular noise,
moving origins, mirror/rigid transforms, independent hands and lifecycle faults.
Results and captures live under ignored `Validation/TrackingLab`; maintained
aggregate measurements and the checkpoint record actual outcomes and limits.

## Measured candidate behavior

The compiled-Udon synthetic measurements are retained in
[udon-range-adaptive-filter.csv](measurements/udon-range-adaptive-filter.csv).
At 30/72/120 Hz, a 90-degree step at 1 km or 1 billion metres reaches 90% of
its target angle in 0.100/0.111/0.100 seconds, after either one or sixty seconds
of stationary history. Its minimum radius during that turn is 70.7-71.3% of
the input radius: chord shortening remains visible and is not claimed solved.

For a subsequent raw point at 0.5 m, the first output is 0.660/0.851/1.103 m
after the 1 km sequence and about 0.5002-0.5007 m after the billion-metre
sequence. This is a first sampled response, not zero continuous-time latency.
The near-only moving-root oracle comparison differs by at most 0.000000735 m
and covariance by at most 0.00000000746. A strict bit-for-bit initial assertion
failed because of these cross-execution floating-point differences; the passing
contract uses an explicit one-micrometre bound and records the actual error.

Fixed-seed white angular noise at 1 km has raw RMS 0.217-0.241 degrees and
filtered RMS 0.0679-0.1188 degrees across those rates. This demonstrates
attenuation of that stimulus, not a comparison with legacy noise suppression
or a prediction of physical hand jitter. Repeated moving-root reversals through
2, 8 and 1000 m pass rigid/mirror checks with maximum relative error below
0.000000632. Real recorded-joint replay has not been run for this combination;
the older contraction-only replay does not establish this candidate's behavior.

Actual post-IK avatar fixtures also exercise curl/fist return, wrist rotation,
native mode cycling, same-mode requests, bypass/resume, suspension recovery,
settings changes and UI contact cancellation. Consult CHECKPOINT for final
platform/export results. No physical feel, performance or multiplayer claim
follows from these synthetic tests.
