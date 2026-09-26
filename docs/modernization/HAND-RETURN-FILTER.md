# Recorded long-range return baseline

The 2026-09-26 temporal audit found an unresolved behavior problem: the
world-position Kalman filter retains very distant history after the raw Bird
point returns to the working volume. It follows the intended equations, but
that does not make the return usable. The explicit fully closed fist clears
the history; ordinary returns can still linger far away. No runtime change or
new APK accompanies this audit. The installed app remains v0.6.

## Method

`tests/UnityQuestTemporalChecks.cs` runs production `BirdCursorState` in real
Unity, with separate state for each hand and each direction setting. It steps
once per tracked recorded sample, in timestamp order. Tracking loss cancels
only that hand. Zero degrees reproduces the v0.5 geometry; 45 degrees evaluates
the v0.6 candidate on the same input. These replays seed from their first valid
sample; they do not invent the filter's pre-recording history.

An independent double-precision scalar recurrence checks the production
filter numerically. The recorded v0.5 outputs are compared only after a shared
fully closed fist establishes a known position and variance. Recorded outputs
are also measured directly, independently of any replay startup assumptions.
The filter is sample-based: timestamps measure return delays, but do not
change its Q/R recurrence.

For this audit, near means raw distance from the contemporaneous hand root
below 4 m. A return starts when raw distance crosses from at least 4 m to below
4 m. It settles when filtered distance also falls below 4 m while raw remains
near. If raw leaves that volume, tracking is lost, or the trace ends first, the
episode is censored. Settling here does not mean the filter has converged to
the raw point or that motion feels correct.

## Actual Unity measurements

The private 19.998-second v0.5 recording has 2880 rows, 1440 usable right-hand
samples and only two usable left-hand samples. It supports right-hand temporal
analysis; it cannot establish left-hand physical equivalence.

| Right-hand series | Raw-near samples | Of those, filtered at least 4 m away | Maximum filtered distance while raw is near |
| --- | ---: | ---: | ---: |
| Recorded v0.5 output | 652 | 273 | 2,146,136.25 m |
| Zero-degree replay | 652 | 273 | 2,146,138 m |
| 45-degree replay | 647 | 273 | 2,385,317.75 m |

Each series has 13 returns: eight settle within the uninterrupted near episode,
and five leave the near volume before settling. The longest measured settling
delay is 0.666626 s. Censored episodes are not zero-delay successes or evidence
of infinite settling time. For example, at recording time 2.027466 s, the raw
point is near while the recorded filtered distance is approximately 592,036 m;
the latter reaches the near volume 0.666626 s later. A later return at 7.791016 s
starts with roughly 2.15 million meters of filtered distance and settles at
the full-fist reset 0.124756 s later.

Validation of the measuring tool:

- 5760 checks confirm advancing/cancelling one hand leaves the other hand's
  position unchanged.
- Maximum relative error against the independent double filter is
  0.00000158933 (zero-degree right) and 0.00000118673 (45-degree right).
- 871 recorded v0.5 samples after a known fist reset agree with the zero-degree
  replay within maximum relative error 0.0000228843. This is a numerical
  comparison, not an assertion of exact float identity or submillimeter error
  at enormous ranges.

The run reports **MEASURED**, with passing reference/isolation assertions.
The bad return behavior remains unresolved. The 45-degree result is a replay
of v0.5 input, not a physical test of the newly installed v0.6 app.

## Reproduce privately

The existing Quest build runner copies the helper into the generated validation
project's `Assets/Editor` folder. For an already prepared project, copy this
single helper there, then run Unity 2022.3.22f1 with:

```text
-batchmode -nographics -buildTarget Android
-projectPath <generated QuestHands2022 project>
-executeMethod UnityQuestTemporalChecks.Run
-birdJointTrace <private JSONL path outside Assets>
-logFile <private log path>
```

Omit `-quit`; the helper exits itself. Results are `joint-temporal-result.txt`
and `JointTemporalChecks/near-returns.csv` in that generated project. It does
not build, install or launch an APK. The raw recording, event CSV and generated
project remain in ignored validation storage; only aggregate findings belong
in public project notes. This temporal audit is separate from the build's
optional unsmoothed geometric replay.

## Next experiment

The first comparison is now complete in [FILTER-CANDIDATES.md](FILTER-CANDIDATES.md).
It favors a smooth history correction for further testing; no production
filter or installed APK has changed.

Evaluate a return-aware policy for filter state acquired in limiting poses.
Candidates include filtering a compressed radial coordinate or smoothly
removing obsolete far-range state as the hand returns. Both change feel and
need comparison before adoption. Preserve ordinary legacy filtering when no
limiting history is involved, continuous transitions, finite extreme reach,
the fist endpoint and rigid/hand-frame invariance. Avoid a hard distance snap
or silently clamping Bird's geometric range. Report trajectory/error and
return timing separately from subjective comfort. The current aiming angle,
plain Inflate preference and independent presentation boundary remain intact.
