# Map rotation and range zoom

The recovered `ScaleWithBird.cs` and map scene use explicit zoom mode and the
square of the Bird range ratio. `BirdRangeScale` preserves that law while moving
it into the independent `Bird3D.UI` assembly. Bird geometry, filtering, clicking
and cursor presentation do not participate in this component.

For an engaged gesture, the desired multiplier is
`clamp(startFactor * (range / startRange)^exponent, minimumFactor, maximumFactor)`.
The default exponent is 2. Range is from the supplied logical origin to the
logical Bird point. The first contact captures both current displayed scale and
range. Re-entering therefore continues from the current size; the recovered
script captured its original scale only once, which could jump on later gestures.

The response follows a linearly changing log-scale target with an exact
first-order update. It is positive, works across large ratios, and has stable
30/72/120 Hz synthetic results. Response 0 is immediate. The response is not a
per-frame DOTween allocation, and withdrawal freezes the displayed size
immediately instead of allowing an abandoned scale tween to finish.

## Conventional authoring

- Keep a fixed Box/Sphere/Capsule/convex Mesh collider outside the scaled target
  hierarchy. Contact uses the finite Bird segment through its front surface;
  a tracked origin already inside also qualifies. This is distinct from the
  spherical rotation component's deliberate **back-surface-only** gate.
- Assign a scale target and logical input components. Uniform multiplication
  preserves the target's original proportions and reflection around its own pivot.
  Set finite positive factor limits containing 1; these are relative to the
  configured rest scale, not world dimensions.
- Optionally assign an owning `BirdMenuPanel`. It must be foreground and owned
  by the input's user. Both hands can share the same user ID; only one owns an
  engaged gesture. A second hand cannot steal it.
- Wire `StartScaling`, `StopScaling`, and `ResetScale` to ordinary menu events.
  Started/Stopped carry the input; Changed carries the displayed multiplier.
  Callbacks observe updated state and can cancel. Configure/rebind between steps.
- Put rotation on a parent pivot, scale on its child, and the interaction collider
  on a separate sibling. The sample selects one mode at a time. Growing content
  therefore cannot move its own engagement boundary.

Tracking loss, owner/focus loss, withdrawal or layout-frame change ends the
current gesture. A new contact rebases. Explicit stop/reset, disabled component,
invalid elapsed time or a frame gap over 0.25 seconds disarms zoom. External scale
writers also disarm it; explicitly reconfigure to adopt a new rest scale. Inputs
must call Cancel on loss; there is no silent-input timeout. Runtime scale changes
are local, not persisted to scene assets.

`BirdMapPreview` is an importable Menu Preview example with a small coastal block
map, fixed wire sphere, ROTATE/ZOOM/RESET/CLOSE controls and mode text. Its
Initialize overload accepts caller-owned input without a camera or desktop
source. The geometry is a scale/interaction illustration, not the historical
map asset or final Bird World art.

## Local Udon adaptation

`BirdUiRangeScale` implements the same contract as an explicit UdonSharp program;
Inspector fields and named local events replace C# configuration and UnityEvents.
Initialize rebases the rest scale after authoring/runtime reconfiguration.
`BirdMapStation` is sample mode orchestration only. Both programs use sync mode
None; network ownership and shared scale/rotation policy are not implemented.

The heavy `BirdMapDemo` extends a copy of the Hanoi scene with a MAP menu branch,
keeping the color selector and paired puzzles. Its fixed interaction sphere is
1.05 m radius; the map zoom factors are 0.3–2.3. Back returns to the parent menu.
Closing/disabling the map cancels both modes. The existing foreground menu gate
continues to keep puzzle gripping separate from UI interaction. Input remains
the explicitly labeled local desktop demonstration source. This is not actual
VRChat hand-input validation or the final world.

## Reproduction and evidence

Run `tests/Invoke-UnityMapChecks.ps1 -UnityEditor <2022.3.22f1 executable>
-ProjectPath <new-or-marked validation directory> -BuildPlayer`.
The fresh Unity project runs range/mode/event tests, the existing spherical
regression, saves a preview scene, builds a Windows player and drives its normal
Update/LateUpdate sequence with synthetic input. Images and logs stay under
ignored heavy `Validation/Map2022`; rate CSVs are preserved in `measurements`.

For the world, run `tests/Invoke-UnityUdonMapChecks.ps1 -UnityEditor <executable>
-ProjectPath <heavy BirdWorld>`. The runner restores maintained source/meta
pairs, compiles programs and drives backing Udon VMs in ClientSim. Add `-Generate`
only when creating the new scene; it refuses to overwrite an existing one.
`-BuildWorld` requests a Windows SDK build-only artifact, never an upload.
The current checkpoint records exact passing counts and build qualifications.

Final ordinary Unity evidence is 71 map/range assertions plus the existing 316
spherical assertions, a prefab event round trip, six editor captures, and a
Windows x64 Mono build/normal-frame player pass with two captures. The compiled
Udon scene has 61 assertions, including a normal-frame menu/mode/zoom/clutch/
rotation/coast/back/loss sequence, map-versus-world-grip gating, six transformed
frames, saved label placement and local callback cancellation. All use Unity 2022.3.22f1; Udon uses
Worlds SDK 3.10.5. No other editor compatibility is inferred.

`measurements/map-range-scale-rates.csv` uses exponent 2, response 10, and a
one-second reach change from 4 to `4*exp(0.5)`: all three tested rates produce
factor 2.45961428. The Udon CSV uses response 12 and a reach change from 6 to
`6*exp(0.6)`: factors are 3.00416827, 3.00416827 and 3.004168 at 30/72/120 Hz.
These are separate analytic fixtures, not a cross-runtime parameter mismatch
or measured physical input timing. Both agree with their independent analytic
solutions within the stated test tolerances.

The final Windows SDK build-only artifact is 261008 bytes, SHA256
`AE3131AF827C7F97C26F5685DEBC545232AE4AC2BFB5CC35D9A31F44501278EE`.
Independent Unity catalog loading finds
`Assets/BirdGenerated/BirdMapDemoBuildValidation.unity`. The SDK still logs the
unexplained internal `Build Finished, Result: Failure.` line despite its API
completion and the fresh catalog-readable file. This remains qualified artifact
evidence, not a clean VRChat client/runtime pass. No Android map bundle, upload,
headset or account operation was performed in this cycle.

Physical map feel, real hand noise/cadence, headset cost, client loading,
multiplayer policy, arbitrary-axis constraints, placement-aware scaling,
obstacle/lift policy and richer visual-state authoring remain further work.
Scaling in this component is for free map/object presentation; it does not
recompute the bounded grip framework's cached full-volume constraints mid-grip.
