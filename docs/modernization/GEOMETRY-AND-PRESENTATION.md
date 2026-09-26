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

- `Integrations/VRChat/BirdSphereFit.cs` accepts points and reports fit conditioning. It knows nothing about rendering, input SDKs, clicks or raycasts.
- `BirdCursorState` adds an optional pose-aware geometric limit law and retains
  the existing range, filter and click convenience
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

## Pose-aware geometric limits

Dana's physical test found sudden cap activation and a closed fist reaching the
maximum sphere. The v0.2-v0.4 palm continuation is withdrawn. Version 0.5 keeps
the original sphere fit separate and blends its center vector with an
independent palm-outward law near extension; a closed-hand endpoint brings the
point to the hand root. Finger articulation distinguishes flat from folded
singular fits. See [hand limits and recording](HAND-LIMITS.md) for the actual
parameters, API requirements, tests and remaining physical-tuning questions.

`useHandLimits` defaults off on `BirdCursorState`. The Quest demo supplies a
verified palm frame and canonical points/root. The avatar adapter does not
enable it. The old `BirdSphereFit.constrainToPalm` fields have been removed.
A sphere diagnostic fades when that fit ceases to define the point.

## Experimental depth presentation

Default choices are a **32 mm physical diameter through 4 m**, an inflation
band from 4 to 20 m reaching 9.5x diameter (v0.8, reduced from v0.7's 11x), then square-root growth. Beyond that
band the solid cursor continues shrinking in apparent size with distance.
Plain inflation is the default after Dana's physical preference report.
Three modes still permit comparison: fixed world size, inflation, and inflation with
0.22 s outward growth lag. Inward resizing is immediate, including a one-frame
return from enormous range to the palm. The working volume, diameter, inflation
band/factor, exponent and lag are designer settings.

A separate outline locates a distant subpixel core. It can be disabled.
Version 0.7 increases its angular diameter to .00525 radians, line width to
.0012 radians and opacity to .85, with a dark contrast edge. These remain
presentation settings, and both outline layers disappear in the near volume.
Each historical trail point receives its own width from its current distance to
the eye; near trail width is 2 mm, with continuous angular visibility scaling
outside the working volume. Its far angular width is now .0016 radians, twice
the previous value; far opacity rises smoothly to .9 while near opacity stays
.65. It does not inherit the tip's size or lag.

The diagnostic renderer preserves real positions through 100 m, then maps
display depth continuously into a shell asymptoting to 500 m. Direction and
angular size use the original logical distance. This avoids the demo's previous
20 m hide threshold without feeding false coordinates to interactions.
It needs a camera far clip beyond that shell. Version 0.8 supplies logical
fragment depth for the core, locator and each trail point, so opaque scenery
can hide a cursor behind distant mountains without clamping Bird's point.
See the [depth policy and actual render evidence](DEPTH-STEREO-OCCLUSION.md).
Very large logical coordinates retain floating-point precision limits.

## Evidence and remaining experiments

The stereo/depth fixture now passes on reversed Direct3D11 and conventional
OpenGL depth, including partial and mixed-trail occlusion. The shared display
shell still adds under 0.13 pixel disparity in that controlled camera setup;
this is not proof of headset stereo comfort, GPU performance or exact binocular
registration. Built-in perspective cameras and opaque scene occlusion are the
current supported presentation case.

The shared hand fixture now tests articulated finger chains, including flat,
closed and planar folded inputs, ordinary legacy parity at shipped parameters,
mirrored/rigid/scale/noise cases and return from distant filter history. It
runs in actual Unity C# and compiled Udon. The old grid-only checks did not
cover the physical failure; synthetic passes are not a substitute for feel.
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

The original Fireball scale script has now been recovered from local Dropbox
work that was absent from Git: `PointerDynamics.cs` uses a 3 cm floor and
`3*ln(1+hand-root range/100)` local scale, without temporal smoothing. See the
[local history and byte-preserved source](LOCAL-BIRD-HISTORY.md). This supersedes
the earlier unsuccessful search of tracked/package scripts. It is a reference,
not the current policy: Dana's later fixed-size working-volume requirement
continues to govern presentation, and local scale is not automatically visible
diameter under arbitrary mesh/parent transforms.
