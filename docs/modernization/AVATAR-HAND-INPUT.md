# Experimental avatar Bird in Tracking Lab 05

This is the first 16-point avatar-input experiment in the authored VRChat lab.
It uses the existing `BirdSphereFit` and `BirdCursorState`, including the accepted
flat/fist limits, polynomial range and optional Kalman recurrence. Its input is
an avatar approximation, not raw tracked hand joints. The older 12-origin
`BirdAvatarInput` and its preview scene remain available as separate diagnostics.

## Try the lab

Use VRChat's ordinary interaction on the console to the left. Open and straighten
one hand, including the thumb, then use the other hand to activate **SET LEFT**
or **SET RIGHT**. Each side can be calibrated independently. VRChat already identifies left and
right; SET learns distal-bone axes because the SDK does not supply fingertip
endpoints. It does not teach the system which hand is which. **RESET** hides that
side's Bird and requires calibration again. White dots show estimated fingertips;
cyan/pink diagnostic dots show sampled avatar bone origins. Orange cubes are
VRChat tracked hand origins, not Bird. The diagnostic dots now render through
avatar skin so their articulation is inspectable; their positions remain exact
SDK bone samples. This X-ray presentation is confined to diagnostics. Bird
itself retains normal world occlusion. A short colored guide from the hand
shows its current direction, bounded to 40 cm, without changing its position.
The console displays curl angle and desired/shown range. Two fixed white
reference spheres compare the Bird material with an ordinary material. A gray sphere in front
of the bench turns green when the logical hand-to-Bird segment passes through it.
No click is required or synthesized in this first input test.

**Point / RAW** is a native VRChat control below the mirror switch. Press it
to compare **RAW** and **FILTERED** using identical hand input, geometry and
presentation. Each mode change seeds fresh filter history without recalibrating.
Lab 05 starts RAW so known extreme-range filter history cannot obscure the input
test. FILTERED remains available for comparison; the filter equations are unchanged.
If desired range is near but shown range remains far, compare RAW before changing
the calibration. If both ranges remain far while curling, inspect the joint/tip
dots and curl readout instead.

**Geometry / ON** toggles a separate X-ray overlay, initially on:

- Gold: three great circles at the actual fitted sphere radius and center, a
  9 mm center marker, and the hand-root-to-center line.
- Green: the palm-facing normal, drawn 8 cm long.
- Cyan/pink: a line ending at the displayed Bird point and an X-ray diamond
  there. The ordinary depth-tested Bird remains present underneath this diagnostic.

The gold fit is neither reflected nor clamped to look plausible. It disappears
when the fitter rejects a singular/ill-conditioned point set; the hand panel
reports that case explicitly. It also shows fitted radius, conditioning and the
hand-limit blend. The final Bird may leave the fit-center ray when the limit law
contributes; these are distinct quantities. At extreme distances, the colored ray
and diamond use the same render-shell endpoint as the point view, rather than
claiming that a billion-meter line is drawn literally. Ordinary near geometry
stays at its actual world position. Toggling this overlay changes no solver input.

Compare white dots against the ends of the avatar fingers, then open/curl the
hand and turn the wrist. A successful calibration only establishes usable data
and an open-pose check; it cannot prove anatomical fingertip accuracy or tracking
confidence. Bone positions may remain available after physical hand tracking is
lost, and controller-driven finger animation can also produce bone positions.

## Input contract

`BirdAvatarHandInput` reads the local wrist and 15 finger bone origins plus five
distal bone rotations every `PostLateUpdate`, after avatar IK. Calibration stores
the preceding segment's direction in each distal bone's local rotation frame.
Later endpoints follow the distal rotation rather than extending the current
middle segment without regard to the last joint. This supports differing bone
axes, provided the hand was actually straight during calibration.

The inferred distal length defaults to 0.8 times the preceding segment. This is
an explicit Inspector assumption, not a measured tip. The canonical fit array is
thumb intermediate/distal/estimated tip, index proximal, then four points each
for middle/ring/little. Index's estimated tip is supplied to the cursor's finite
input contract, but `clicksAllowed` is forced off until click fidelity is tested.

The root keeps Bird's 60% index-proximal / 40% thumb-proximal mapping. Palm normal
uses ordered thumb/index/little origins with a handedness correction, without
head/torso/world-up extrapolation. The calibration-only open-pose check uses the
wrist-to-knuckle direction; a lateral thumb base would reject wide straight hands.
The normal's physical orientation and the humanoid bone correspondence still
need on-headset verification for a given avatar.

Lab 05 corrects the anatomical winding sign: the ordered thumb/index/little cross
product faces the back of a right hand, so the palm sign is right -1 / left +1.
Dana observed RAW rays pointing out the backs of the hands. An aggregate check
of the earlier private OpenXR recording found the old sign opposite the host's
palm-orientation-corrected normal in all 1,442 tracked samples (1,440 right, two
left). The standalone host already corrected winding from tracked palm rotation;
the VRChat adapter omitted that correction. The original synthetic fixture had
reversed left/right anatomy and masked this bug. The corrected fixture asserts
an independently specified palm-facing normal through flexion and rigid rotation.

Mean middle/ring/little finger length normalizes range input to an authored 9 cm
reference. The limit-law endpoint is expressed in the same units, preserving its
billion-meter reach across uniform avatar scales. No preview range clamp is
applied. This normalization changes the input interpretation, not the solver's
range polynomial. Avatar proportions and inferred tips can still change feel.

Missing/invalid bones or distal quaternions cancel and hide the cursor immediately
but retain calibration. The status reads **Paused / SET retained**. Valid samples
resume with fresh filter history and need no new SET. A sampling pause longer
than 250 ms also discards old filter history, since suspension may stop Udon
without supplying an invalid sample. This is a recovery rule, not an adaptive
smoothing algorithm.

Local SDK avatar-change events, disable, handedness/calibration-setting changes
and explicit RESET still clear calibration. Explicit SET attempts replace the
previous calibration, including when the new attempt fails. Remote avatar events
do not clear local calibration. Retention relies on the SDK's
[avatar-change event](https://creators.vrchat.com/worlds/udon/avatar-events/);
the adapter does not independently query avatar identity. No profile or player
data is recorded or networked.

## Presentation and timing

`BirdLabPointView` is optional and separate from point geometry. It follows the
same-frame sample at post-IK order 100; the input runs at 0 and the point-through
exercise at 200. Existing UI consumers that run in ordinary LateUpdate need
deliberate post-IK scheduling before being connected; otherwise they read the
previous frame. The simple exercise here consumes the current logical position.

The visual uses the unchanged `Bird/LogicalDepth` shader, the preferred 32 mm
near diameter through 4 m, 9.5x mid-range inflation and a separate far locator.
Very distant visuals project into a 500 m render shell while depth testing against
their logical distance. Projection never feeds the solver or target. This initial
lab view has no trail. It does not replace the richer standalone presentation.

## Reproduce

From the light repository:

```powershell
./tests/Invoke-UnityTrackingLab.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/BirdWorld' -Check -CheckBird
```

Add `-Launch` only when switching the headset into the new lab is useful.
The standalone v0.10 installation remains available and unchanged.
`-AddBird` is a one-time authoring operation and refuses an existing integration.
`-RefineBird` updates the existing lab's authored diagnostics and native comparison
control; it preserves the other demo scenes.
Sources and shader GUIDs restore into ignored `BirdGenerated`; authored scene,
materials and Udon program metadata live in the heavy repository.

Compiled-Udon checks pass 13,682 assertions over 300 normal frames. Both default
ClientSim robot hands calibrate and run Bird; their inferred mean finger length
is about 0.11645 m and their near-flat pose selects the far limit. Actual SDK
avatar scaling to 0.5x/1.5x/restored size preserves normalized range. Controlled
SDK position/rotation fixtures cover arbitrary distal axes, mirrored/upside-down
hands, 0.5x/1.5x/3x sizes, articulation from flare through fist, 30 changing
consecutive frames, 36 independent data faults, disable/recovery, native controls,
remote/local SDK avatar event routing and the logical target. Missing-data tests
check retained calibration, hidden visuals and recovery without stale far history;
a controlled stale-timestamp fixture checks the sampling-gap path. It is not an
actual headset suspend/resume test. Native RAW/FILTERED switching checks mode
isolation, disabled controls, fresh history and unchanged geometry. The full fist
returns to the root even after billion-meter filtered history. Event replay is
not an actual avatar replacement; synthetic bones are not physical tracking.

The standard diagnostic test also passes its 34-marker cadence/lifecycle checks.
Rendered controls also verify normal material occlusion, X-ray visibility
through an opaque object, and the Bird point material at a known visible
position. The short guide is checked against the logical point and its 40 cm
bound. Saved/reloaded UI label anchors are checked against their backing panels;
camera renders are inspected. Lab 05 also checks corrected anatomical normals,
actual sphere-center/radius agreement, singular-fit hiding, the displayed Bird
ray endpoint and geometry-toggle isolation. The native toggle test allows the
SDK's queued PostLateUpdate re-registration before requiring resumed updates.
Isolated camera checks render the actual Udon point/halo at near and extreme
ranges, and verify that a foreground wall occludes the ordinary point view.
The geometry inspection overlay is deliberately X-ray. Final SDK build evidence
is in CHECKPOINT.md.
Lab 03 was subsequently loaded through normal SDK Android BuildAndTest. An
actual stereo device capture shows the console running, 16/16 bone availability,
left-hand calibration accepted and Bird active, with the right hand awaiting
calibration. This proves the adapter executes in-client and accepts a physical
calibration; tip accuracy, pointing feel and clicking remain unvalidated.

Lab 04's compiled-Udon pose sequence reproduces a large lag: immediately after
moving from a flat far pose to a 90-degree curl, raw range is 1.277 m while the
filtered point remains about 87.5 million m away. Switching RAW shows 1.277 m on
the next normal frame. See [synthetic temporal measurements](measurements/avatar-hand-temporal.csv).
This establishes a possible contributor to an absent nearby point, not the cause
of Dana's physical report. The filter equations remain unchanged. Lab 04 passed
normal Android SDK BuildAndTest; its 167,798-byte bundle hash matches the Quest
copy. Dana later confirmed the joint markers visible; Bird was still absent and
RAW rays pointed out the backs of the hands, motivating Lab 05's sign fix and
requested geometry overlay.

Lab 05 passed normal Android SDK BuildAndTest; the 178,385-byte bundle hash
matches the Quest copy. The headset was asleep during deployment, so its live
geometry view and corrected physical response remain unconfirmed. Reload and
SET each open hand before assessing the sphere while curling.
