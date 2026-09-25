# Outward size growth experiment

The optional depth renderer keeps a 32 mm physical cursor through 4 m. Outside
that volume, its lag mode smooths increases in the chosen visual diameter.
This changes presentation only, with no effect on Bird's logical point,
sphere fit, range polynomial or Kalman filter.

## The measured problem

The previous filter used the newly sampled target diameter for the entire
preceding update interval. On rapid outward motion, longer intervals therefore
overestimated growth. The real-renderer motion fixture reproduced a **23.124%**
spread between simulated 30/72/120 Hz updates at the same point in its stroke.

## Integration between samples

The renderer now remembers the preceding target diameter and assumes positive
targets change exponentially between samples (linearly in log diameter).
For an outward segment, it integrates the first-order response exactly under
that assumption:

```text
h = dt / lagSeconds
g = log(targetNow / targetBefore)
diameterNow = diameterBefore * exp(-h)
            + targetNow * (1 - exp(-(h + g))) * h / (h + g)
```

Zero elapsed time does not advance growth. A small-argument series avoids
cancellation. Results stay between the current and target diameters. With no
previous sample, the helper retains its original constant-target step behavior;
the renderer resets target history on clear/recovery and mode changes.

When the target decreases, growth stops. If the target is already smaller than
the displayed diameter, the cursor shrinks to it immediately. Entering the
working volume always restores the configured physical diameter immediately.
Holding position still lets a lagging cursor grow toward its target. Thus no
late outward catch-up growth continues while a decreasing target returns home.

This is exact for exponential target segments, not a claim of identical
behavior for arbitrary unseen motion between updates. It approximates other
trajectories, and cannot reconstruct movement missed during a tracking gap.

## Measured comparison

The fixture moves the logical cursor from 0.4 m toward one million meters.
At t=0.5 s its logical distance is 3988.16 m. Values below are logical visual
diameters before the display-depth projection, not giant objects near the eye.

| Simulated rate | Previous diameter | Integrated diameter |
| --- | ---: | ---: |
| 30 Hz | 0.951481 m | 0.716249 m |
| 72 Hz | 0.811190 m | 0.716673 m |
| 120 Hz | 0.772785 m | 0.716778 m |

The spread is now **0.074%**. The lag remains an experimental 0.22 seconds;
it has not been retuned to compensate for the previous interval bias.

The real Unity fixture also checks all three size modes over 1341 deterministic
renderer updates, including a 0.12 s return from one million meters, a one-update
return from a billion meters, attached trail tips, selection and clear/recovery.
Near diameter stays 32 mm and historical near-trail width stays at most 2 mm.
Eighteen snapshots include the actual generated geometry.

Independent math checks use closed-form constant/exponential target solutions
and irregular intervals. A linear target ramp is checked against its continuous
analytic solution with a 0.2% relative-error bound at 30/72/120/240 Hz.
These tests do not establish headset frame pacing, subjective feel, stereo
depth or world occlusion. Those remain separate experiments.
