# Standalone feedback: far response and intentional contact

Follow-up: the VRChat lab now has an explicitly optional
[range-adaptive comparison](RANGE-ADAPTIVE-FILTER.md), with compiled-Udon and
SDK export evidence. RAW and the original recurrence remain available; no
standalone APK or deployed headset revision changes are implied.

Dana's 2026-09-27 feedback explicitly concerns standalone Quest v0.10, not the
VRChat tracking lab. Dana subsequently clarified that VRChat remains the project priority: these
ideas should be preserved and explored in a bounded experiment, but may be
deferred as research rather than delaying the world. Dana likes the standalone
experience, but reports unpredictable,
long angular lag during far wrist motion; full-fist return already feels correct.

## Smoothing requirements

- Preserve normal, locally Euclidean smoothing throughout the hand/arm's-reach
  and several-step working volume. Avoid origin singularities, jumps and strange
  near-hand dynamics.
- Far wrist/hand angular changes should move the distant point quickly. Enormous
  world-space velocity is expected at great range, not a reason to suppress it.
- Any adaptive parameters must use the incoming noisy logical Bird point's
  distance from the hand, never the delayed displayed/filtered point. Otherwise
  the filter can remain stuck waiting for its own lagged state to catch up.
- Explore a smooth hand-centered invertible map Z, filter Z(raw Bird), and map
  back through its inverse. Keep Z identity in the working volume and compress
  far radial distance approximately logarithmically. Treat a moving origin and
  state transport explicitly.
- A radial compression followed by ordinary Cartesian averaging can shorten
  radius during a pure angular turn; the inverse can magnify that shortening.
  Test this before adopting a map. Also compare decoupled angular/radial filtering
  and the simpler option of range-dependent Kalman process noise.

The current recovered `Bird.cs` uses fixed Q=.001 and R=270*d^3, where d is the
pre-polynomial center distance. `BirdCursorState` preserves that recurrence.
The very large input of the new flat-hand limit therefore makes R large and gain
small; covariance history also changes the transient response. Dana remembers
range-dependent process-noise tuning in earlier experiments. Preserve the exact
source distinction rather than assuming that remembered variant is already here.

## Contact requirements

A spherical selector must acquire scrolling only after the Bird point is
observed inside it and then moves outward through its BACK surface, extending
away from the hand. Being beyond the sphere while its ray crosses the sphere is
insufficient. Moving sideways past a foreground selector with a distant Bird
must not start it spinning.

Once contact is lost, require a new interior visit and outward piercing before
scrolling can resume. Withdrawal stops driving; existing momentum may coast.
Loss of tracking, eligibility, ownership or the interaction's lifecycle also
invalidates any pending contact. Startup outside must not arm a selector.

Keep arming/contact local to each pointer and each object. A user should be able
to operate a building-size selector behind ten smaller foreground selectors
without disturbing the foreground. Do not make unvisited foreground objects
capture a ray or swallow the background object's input. The Bird is a 3D point;
ray-like manipulation is a selectively acquired relationship with an object.
This general principle should inform later touching/gripping tools as well.

Tests must include outside-to-outside sweeps, inside-to-back exits, exits through
the front/side, missed-sample jumps, contact loss/reentry, two hands, transformed
and moving volumes, tracking/owner changes, callback/lifecycle changes and nested
near/far selectors. A coarse sample that jumps completely through a sphere
without any observed interior visit should not invent contact.

Implemented in the maintained ordinary Unity and Udon spherical-scroll components
on 2026-09-27. The shared contract tests include one background selector behind
ten untouched foreground selectors. See [acquisition details](SPHERICAL-SCROLL.md#deliberate-acquisition-2026-09-27)
for margins, adjacent-sample requirements, stale input and fixed-volume policy.
This does not change the fitting or filtering laws. The installed standalone
v0.10 and Quest Lab 05 remain unchanged by this contact-policy checkpoint;
physical contact feel is pending.

## Bounded numerical study (2026-09-27)

`python tests/compare_far_filter.py` reproduces
`measurements/far-filter-angular-study.csv`: stationary hand origin, an abrupt
90-degree direction change at constant raw range, 30/72/120 Hz, and 1/60 seconds
of stationary filter history. This deliberately isolates angular response; it
is not hand tracking, runtime timing, noise rejection or subjective validation.

The radial map tested is identity through R=4 m and, outside that region,
f(r)=R+L*asinh((r-R)/L), with L=4 m. It has a C2 join and explicit sinh inverse,
and is asymptotically logarithmic. These conditions do not select a unique map.
Applying the same scalar Kalman gain in this compressed space does not improve
the constant-range angular response. It amplifies chord shortening: at a billion
meters the interpolated radius falls to about 0.21% during a 90-degree turn,
versus 70.7% in ordinary Cartesian interpolation. A much faster gain leaves that
geometric problem in place. A moving hand origin remains an additional question.

A separate candidate floors the predicted covariance at R_noise*k/(1-k), where
k=1-exp(-dt*w(rawRange)/0.05) and w smoothly rises from zero at 4 m to one at
20 m. This is equivalent to increasing process noise from incoming raw range.
It preserves the existing gain whenever that gain is already faster. At 72 Hz,
after 60 seconds stationary, reaching 90% of a 90-degree turn takes:

| Raw range | Existing recurrence | Candidate covariance floor |
| --- | --- | --- |
| 4 m | 0.278 s | 0.278 s |
| 1 km | 1.125 s | 0.111 s |
| 1 billion m | 35.292 s | 0.111 s |

All tested trajectories wholly within the working volume match the original
recurrence. This does not establish equivalence on return from far history:
the covariance has changed. Jitter amplification, moving origin, loss/recovery,
continuous wrist motion, near/far transitions and private trace replay need
assessment before promotion. No candidate is enabled in Lab 03 or standalone
v0.10. The bounded result favors investigating a simple adaptive covariance
before committing to a compressed-coordinate filter. Continue VRChat delivery;
this research is not a gate for the world or its next interaction milestone.
