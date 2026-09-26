# Comparing return-filter policies

The 2026-09-26 05:51 UTC cycle adds an editor-only experiment in
`tests/UnityQuestFilterExperiments.cs`. It uses current production 45-degree
geometry and the private recording. No runtime source, original `Bird.cs`,
Udon program, visual policy or installed v0.6 APK changes in this checkpoint.

## Candidates and decision

All three retain the existing scalar Q=.001 and R=270*d^3 Kalman recurrence,
startup/recovery seeding and full-fist endpoint. The baseline is checked against
the real production cursor, with maximum relative difference 0.000000530782.

1. **Legacy** filters world position directly, as currently installed.
2. **Log-radial** expresses both previous output and raw target relative to the
   current hand root, leaves radius through 4 m unchanged, and maps larger
   radius r to `4 + 4*log(r/4)`. It filters these vectors, then inverts the map.
3. **Elastic history** retains world-position filtering, but smoothly reduces
   obsolete outward history before that step. Let `s = current raw range + 4 m`.
   Historical radius at or below s is unchanged. Above s, its excess e becomes
   `e / (1 + e*dt/(width*tau))`, with experimental width=1 m and tau=1/72 s.
   The history correction is blended by the largest flatWeight seen since the
   last return inside 4 m. Tracking recovery reseeds this memory. A tiny flat
   contribution therefore causes a tiny correction instead of switching the
   full policy on. No head/torso frame or camera distance participates.

The elastic candidate bounds excess *filter history relative to the current
target*, not the geometric point or maximum target range. Its radial map is
continuous with matching first derivative where correction begins. Remembered
influence is cleared in a region where the radial correction is already the
identity. Width, time and 4 m margin are experimental parameters, not approved
defaults. Partial-influence history and noisy reversals need more assessment.

**Carry elastic history forward for further testing. Do not promote this
log-radial candidate.** Both improve returns, but retaining the original gain
with a compressed signal makes outward extension far too weak in the synthetic
comparison. This rejects this particular gain/mapping combination; it does
not rule out every possible nonlinear filter.

## Recorded-input result

Actual Unity 2022.3.22f1 execution, 1442 usable samples: 1440 right and two left.
The right-hand comparison uses identical current 45-degree raw input for all
three candidates. There are 647 raw-near samples and 13 returns. Definitions
of near, settled and censored are in [the baseline audit](HAND-RETURN-FILTER.md).

| Policy | Near samples with filter still outside 4 m | Maximum filter range while raw is near | Settled / censored returns | Longest settled delay |
| --- | ---: | ---: | ---: | ---: |
| Legacy | 273 | 2,385,318.75 m | 8 / 5 | 0.666626 s |
| Log-radial | 38 | 8.772564 m | 13 / 0 | 0.111084 s |
| Elastic history | 45 | 7.783112 m | 13 / 0 | 0.152710 s |

This is replay evidence, not a recording of the candidate in use. Settled
means entering the near volume during that uninterrupted raw-near episode;
it does not mean exact convergence or proven comfortable motion. The left-hand
recording is insufficient for a physical temporal comparison.

## Synthetic evidence and limits

The controlled trajectory holds at 0.5 m, extends exponentially to roughly
one billion meters over one second, holds there for one second, returns in
0.2 s, then stays near. It includes a moving hand root and changing direction.
Sample rates are 30/72/120 Hz, not hardware frame-rate measurements.

At 72 Hz, after the one-second far hold, legacy and elastic history both reach
about **4.61 million meters**. Log-radial reaches only **17.33 m**. Elastic
matches the entire outward/hold baseline trajectory within the checked relative
tolerance 0.000001. Return-to-near delays for elastic are 0, 0 and 0.016667 s
at 30/72/120 Hz; legacy takes longer than the remaining near episode at 30 Hz,
0.861111 s at 72 Hz, and 0.541667 s at 120 Hz. Zero here means the first sampled
near input already has a near output, not zero latency in continuous motion.

Other passing checks:

- Elastic with no flat contribution preserves the entire synthetic near/far
  legacy trajectory exactly. Across the three candidates, ordinary near-only
  history differs from legacy by at most 0.000000092159 m.
- Mirrored and rigidly transformed trajectories: maximum relative error
  0.00000598962. These exercise the filter, independently of geometric tests.
- Fully closed fist and tracking recovery retain the exact required endpoint.
- At first activation with flatWeight=0.000001, relative correction is
  0.0000010098, checking against an abrupt full-strength switch.
- For a fixed target and full correction influence, splitting 0.2 seconds into
  30/72/120 Hz or irregular final steps agrees with the analytic excess-flow
  solution within 0.000000476837 m.

Only that isolated contraction flow is time-consistent by this test. The
retained Kalman Q/R recurrence remains sample-based, and varying targets plus
partial influence can introduce further sample-rate dependence. These tests
do not establish perceptual continuity, click behavior, performance or Udon
compatibility of a future runtime implementation.

## Reproduction and next checkpoint

The Quest project runner copies this helper and its shared return-metric
helper into `Assets/Editor`. With an already prepared generated project, copy
those two helpers there and use the same invocation as the baseline audit,
changing `-executeMethod` to `UnityQuestFilterExperiments.Run`. Keep
`-birdJointTrace` outside Assets; omit `-quit`. No build or device is required.
The method writes `filter-experiments-result.txt` and private per-event
`JointTemporalChecks/filter-experiment-returns.csv`.

The [saved aggregate report](measurements/filter-candidates-20260926.txt)
contains synthetic and summary metrics only. Raw joints and per-event outputs
remain ignored in private validation storage.

Next assess partial flat histories, noisy range/direction, repeated reversals
and rendered return trajectories, including cursor/trail sizing. If the policy
is promoted, expose/reset sample timing deliberately, retain opt-in scope,
check selection/cancellation/loss and run the actual compiled Udon suite before
building and deploying. Preserve the original core and keep physical comfort
separate from all automated evidence.
