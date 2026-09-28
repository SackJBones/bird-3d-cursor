# Mid-range stability assessment, 2026-09-28

Dana reports good near-hand behavior and visual inflation, but violent radial
motion at room-to-two-room distances. This cycle measures the current chain and
one bounded parameter hypothesis. **No production filter, geometry, range law,
prefab default or presentation setting is changed.** Stronger blending alone
does not earn a rollout from these results.

## Reproduction and scope

`UnityMidRangeLabChecks` runs through compiled Udon as part of
`Invoke-UnityTrackingLab.ps1 -Platform Both -CheckBird -SkipBuild`. It extends the
existing original-Kalman, symmetry, near-identity and lifecycle regressions.

The controlled comparison inverts the actual reference polynomial to place an
ideal sphere center at logical ranges 3, 4, 6, 8, 12, 20 and 50 m. It compares
the saved 20 m full-effect distance with 12 m and 8 m, retaining the unchanged
4 m start, Q/R, polynomial and downstream ADAPTIVE filter. Each runs at explicit
30/72/120 Hz. Uniform independent center perturbations have a +/-2% bound on each
axis; the correlated case adds a 2 Hz radial sine of 2% center length. Those are
deliberate synthetic probes, not estimates of actual Quest tracking noise.

The test records radial RMS error against the ideal target, signed range bias,
95th-percentile per-sample radial jumps, angular RMS, 15/90-degree turn settling,
minimum range during turns, and half/double range-step settling. It restores
saved parameters afterward. Near/far transitions use the incoming unfiltered
range, never the delayed displayed point. The degree values describe test
rotations; the runtime filters remain Vector3 based and store no Euler angles.

A second experiment solves for articulated hand poses that reach 3, 6, 8, 12
and 20 m through the actual avatar adapter, fitted sphere, hand limits, original
polynomial and both filters. Mirrored hands receive the same independent
perturbations of up to 0.3 mm per axis on each of 15 finger bones; one uses the
saved 20 m setting, the other 8 m. It runs in normal editor frames and does not
claim a controlled headset cadence. Both sides retain valid poses, and all 900
samples per side have valid sphere fits in the Windows run. This excludes the
singular-fit fallback as the cause of noise in *these particular fixtures*,
not in every real hand pose.

## Controlled results at 72 Hz

Both Android and Windows editor-target suites pass **390,859 assertions over
2,929 normal frames**, with exit code 0. The 1,008-row controlled datasets are
byte-identical (SHA256 `24C9D1693B9FB6CFF18067F6BC06AC5F949693EEB008070708B2242A91039583`).
Curated measurements and result/exit files are in the heavy repository at
`Reference/MidRangeAssessment/20260928`. Android is restored as the editor
target. This is a test-only extension: no world rebuild or new runtime was
needed, and no claim of device numerical/physical acceptance follows from it.

| Logical range | Saved / earlier full effect: radial RMS, white noise | Saved / earlier: radial RMS, correlated noise | Saved / earlier: 15-degree turn to 90% |
| --- | --- | --- | --- |
| 6 m | 7.46 / 7.36 cm | 24.52 / 23.43 cm | 361 / 389 ms |
| 8 m | 11.27 / 10.34 cm | 39.62 / 37.54 cm | 319 / 347 ms |
| 12 m | 20.61 / 20.16 cm | 75.57 / 75.02 cm | 208 / 208 ms |

Here “earlier” means full effect at 8 m, versus the saved 20 m. The improvement
is small, and the extra lag is measurable. At 8 m, a deliberately abrupt
90-degree turn lowers logical range to 51.5% of its starting range with the
saved chain, and **40.0%** with earlier full effect. At 6 m those values are
64.0% and 48.5%. These step tests expose a mechanism; they are not a model of
every normal wrist movement or a claim that Dana made exact 90-degree steps.

The Windows avatar-bone comparison also fails to support a blanket improvement:
radial RMS changes from 9.33 to 9.91 cm at 6 m (worse), 13.18 to 12.37 cm at 8 m,
and 22.64 to 20.93 cm at 12 m. The 3 m results agree to roughly a micrometre of
RMS despite mirrored float arithmetic; the pre-existing exact near-chain
identity regression remains the stronger proof of the unchanged near path.
Editor-cadence measurements may differ between targets; retain both datasets.
Android likewise worsens at 6 m (9.33 to 9.81 cm), with small improvements at
8 m (14.27 to 13.78 cm) and 12 m (24.20 to 22.74 cm), and no singular samples.

## Interpretation and next bounded step

The sixth-power term is extremely sensitive to center length: where it
dominates, a 1% center-length error produces roughly 6% range error. Meanwhile,
Cartesian averaging of equal-length vectors cuts their chord. A 50/50 average
across a 90-degree turn has length `r / sqrt(2)`; applying a sixth power gives
only `1/8` of the far-term range. The downstream Cartesian filter and return
protection add their own dynamics. This explains why “filter harder” can make
radial pumping on turns worse even when stationary samples look quieter.

The next filter experiment should separate radial steadiness from directional
response, with the original Vector3 Kalman as a measured comparator, while
preserving the exact accepted near path. Reuse the existing intrinsic S2
log/exp and SE(3)/reflection checks from `SPHERE-FILTER-LAB.md` rather than
inventing Euler charts. Do not silently enable that older experimental policy:
it did not establish identical near-hand feel or physical acceptance. A candidate
must improve room-scale radial error **and** turn contraction without unacceptable
aim/closing delay. Keep sphere quantities, automatic setup, exact fist return,
history cancellation and the current visual sizing. Also measure bias and
per-frame excursions; RMS against an ideal pose alone does not distinguish them.

This is one bounded investigation, not a reason to hold the VRChat world hostage
to filter research. No good candidate is ready from this parameter comparison;
continue the planned pit/cliff repairs and other independent world work. Device
rendering, physical feel and real multi-client acceptance remain separate.
