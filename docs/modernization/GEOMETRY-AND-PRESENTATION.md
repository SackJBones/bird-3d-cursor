# Bird geometry and optional embodiments

Bird defines a point. The standard implementation chooses fit points, a sphere
fit, a range function and filtering parameters. A mandala, a UI pointer and a
sun-position control can consume that same point with entirely different
interaction and appearance.

Keep dependencies flowing in one direction:

```text
tracked joints -> geometric fit -> range / optional filter -> logical point
                                                           |           |
                                            interaction policy     presentation
                                            clicks, ray hits       marker, trail,
                                            object constraints     mandala, effects
```

Presentation never feeds its projected position, size, smoothing or hit marker
back into the geometric point. A raycast uses the logical point/direction.
Whether to stop at a hit, drag an object, draw a mandala or indicate selection
belongs to the experience. Cursor visuals do not define an interaction volume.

## Current code boundaries

- `Integrations/VRChat/BirdSphereFit.cs` accepts points and an optional palm
  frame. It knows nothing about rendering, input SDKs, clicks or raycasts.
- `BirdCursorState` retains the existing range, filter and click convenience
  state for compatibility. Callers can disable clicks. Legacy `Bird.cs` also
  retains its public convenience APIs; this change does not claim to have
  separated every historical class.
- `Runtime/Presentation/BirdDepthVisual.cs` is an optional, independent
  `Bird3D.Presentation` assembly with no reference to Bird runtime, hand SDKs or
  Udon. It accepts a logical point, camera and externally chosen selection cue.
  `BirdDepthStyle` contains experience choices, not Bird constants.
- `tests/UnityQuestHands.cs` is a comparison experience: it selects a tracking
  adapter, composes math and presentation, and owns diagnostic labels and mode
  buttons. The renderer is ordinary Unity C#, not a compiled-Udon component.
- Existing `BirdTrail` and `BirdRadialTrail` are unchanged. A mandala need not
  instantiate a UI cursor or adopt its depth/size mapping.

## Experimental palm-side continuation

`constrainToPalm` defaults to false. A caller opting in supplies a front-facing
palm normal and root. The Quest comparison derives a cross product from thumb,
index and little-finger bases, mirrors handedness, and uses the tracked palm
orientation to disambiguate the sign when available. The OpenXR joint convention
places +Y toward the back of the hand, so palm front is -Y:
[OpenXR specification](https://registry.khronos.org/OpenXR/specs/1.1-khr/pdf/xrspec.pdf).
The VRChat avatar adapter does not yet provide a verified equivalent frame and
does not enable this option.

The fallback regresses palm height on tangent coordinates and squared distance.
Its signed curvature stays finite when the points approach a plane. Positive
curvature uses reciprocal center distance, transitioning through a C1 shoulder
to a finite endpoint as curvature approaches zero. Negative curvature retains
the front-side continuation rather than reflecting an already inverted center.
A smooth confidence blend preserves the original fit for ordinary well-curved
poses; the result remains in the front half of the configured center ball.
This is an experimental continuation objective, not a claim that a unique
least-squares sphere exists for a plane.

`maximumCenterDistance=2 m` is a numerical sphere-center endpoint. The unchanged
polynomial maps it to about **1.76 billion meters** of logical cursor reach.
It is not a 2 m cursor limit. True infinity is not represented in a float; an
experience can consume the direction as a ray. Invalid/collapsed input is still
rejected. The fitted sphere stays palm-side; the filtered point can lag a moving
palm because the original Kalman recurrence is retained.

## Experimental depth presentation

Default choices are a **32 mm physical diameter through 4 m**, an inflation
band from 4 to 20 m reaching 8x diameter, then square-root growth. Beyond that
band the solid cursor continues shrinking in apparent size with distance.
Three modes permit comparison: fixed world size, inflation, and inflation with
0.22 s outward growth lag. Inward resizing is immediate, including a one-frame
return from enormous range to the palm. The working volume, diameter, inflation
band/factor, exponent and lag are designer settings.

A separate thin outline locates a distant subpixel core. It can be disabled.
Each historical trail point receives its own width from its current distance to
the eye; near trail width is 2 mm, with continuous angular visibility scaling
outside the working volume. It does not inherit the tip's size or lag.

The diagnostic renderer preserves real positions through 100 m, then maps
display depth continuously into a shell asymptoting to 500 m. Direction and
angular size use the original logical distance. This avoids the demo's previous
20 m hide threshold without feeding false coordinates to interactions.
It needs a camera far clip beyond that shell. It is **not a complete solution
for occlusion against distant world geometry**: a host must supply a suitable
overlay/depth policy, or use an appropriate world renderer. Very large logical
coordinates also retain floating-point precision limits.

## Evidence and remaining experiments

The shared palm fixture runs in actual Unity C# and compiled Udon: 1601 noisy
curvature sweep samples, 801 billion-meter reach samples, mirrored hands, rigid
transforms, fresh flat input, original-fit preservation, invalid input and
recovery. These are synthetic geometry tests, not recordings of human hands.
Visual math checks cover constant near size, immediate return, onset continuity,
outward lag and direction-preserving finite projection through 1e12 m.

`UnityDepthVisualChecks.Render` captures all three modes at seven distances from
0.4 m to one million meters. Static images establish visibility and clipping
behavior; they cannot establish perceived depth, comfort or the feel of motion.
The motion fixture below extends this to timed geometry; stereo views,
occlusion and Dana's assessment after returning are still needed.

The motion fixture now exercises the real renderer over 1341 updates with
simulated 30/72/120 Hz timestamps. A 0.12 s return from one million meters and
a one-update return from a billion meters retain the 32 mm near cursor and
at most 2 mm near trail width. Selection and clear/recovery are included;
18 snapshots expose the actual mesh. This is deterministic time stepping,
not a measurement of hardware frame pacing or a perceptual motion study.

That fixture exposed a detached trail tip at 72 Hz: history sampling occurred
every 14 ms, so some cursor updates had no matching trail endpoint. The latest
sample now follows the point every update, using a separate sampling clock so
new history continues to accumulate. A regression checks endpoint attachment
and continued bounded history at every tested rate. Stereo, occlusion and
actual perceived depth remain the next experiments.

The previous growth filter showed 23.124% diameter spread across update rates
on the extreme outward sweep. Integrating the changing target between samples
reduces that measured spread to 0.074%, while preserving immediate return
sizing. See [the growth experiment](DEPTH-GROWTH.md) for the model, rate
comparison and analytic checks. This improves numerical consistency; equal
perceived feel across rates is still not established.

[LenSelect](https://www.frontiersin.org/journals/virtual-reality/articles/10.3389/frvir.2021.684677/full)
studies dynamically scaling selectable objects to improve acquisition. It is
useful adjacent work on visibility/selection, but does not validate Bird's
cursor-size curve or lag. Those remain experimental design choices.

The tracked Fireball scripts, earlier root Bird scripts, initial Fireball
`Bird.cs` at heavy-repo commit `327069e`, and packaged plugin scripts inspected
so far do not contain the remembered marker inflation code. The initial Fireball
script has a different range polynomial and scales the debug fit sphere only.
Do not claim that this new presentation reproduces the historical experiment.
