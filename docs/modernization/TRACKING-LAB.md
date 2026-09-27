# VRChat tracking laboratory

Current authored revision: **Lab 13**, with automatic fingertip estimates and
optional REFINE/AUTO correction. Bird needs no SET step. ADAPTIVE/PALM defaults
and Lab 12's sphere-center direction remain unchanged. Both platform builds and
compiled-Udon checks pass; this revision has not been loaded on Quest, which
retains Lab 12. See [automatic setup](AUTOMATIC-AVATAR-SETUP.md) and CHECKPOINT.md.
The numbered milestones below describe their historical behavior.

`BirdWorld/Assets/BirdWorld/Scenes/BirdTrackingLab.unity` is the first-client
milestone: a normal authored VRChat world for inspecting avatar hands and
trying experimental calibrated Bird input. It is separate from the standalone OpenXR comparison and the
desktop-input interaction demonstration scenes.

The world provides a protected floor/spawn, meter grid, 10/25/50 cm objects,
distant range references, a work bench, a switchable SDK mirror and local hand
diagnostics. Cyan/pink spheres show avatar bone origins; amber cubes show the
SDK's left/right TrackingData origins. Bone counts indicate availability, not
tracking confidence. Distal bone origins are not fingertip endpoints. The console
reports avatar eye height and hand-origin/wrist separation. Lab 03 adds an
explicitly calibrated avatar-finger adapter, estimated fingertips, Bird point
and point-through target. See [avatar input](AVATAR-HAND-INPUT.md) for controls,
assumptions and checks. It does not record hand data or modify the player.

Lab 04 added a native **Point / FILTERED** control to compare RAW output,
retains SET calibration across invalid samples, and starts fresh filter history
after missing data or a sampling pause. Avatar changes still require SET. The
comparison shares one solver and presentation; it does not promote a new filter.
The diagnostic panel distinguishes desired and shown range. Synthetic compiled
Udon reproduces severe legacy lag after a far pose, but the physical missing-Bird
report remains unresolved. The new bundle is transferred and hash-verified on
Quest; the headset was asleep before its view could be confirmed.

Lab 05 responds to Dana confirming visible joint markers but RAW rays pointing
out of the backs of the hands. It corrects the adapter's palm-normal winding
sign and starts RAW. **Geometry / ON** exposes the actual fitted sphere and
center in gold, the palm normal in green, and the resulting Bird ray/diamond
in the hand's color. This optional X-ray layer changes no solver input. A singular
fit hides its gold geometry and is reported in the hand panel; it is never
replaced with a fictitious fitted sphere. See the avatar-input document for the
render-shell distinction and the latest checkpoint for deployment evidence.

The authored Lab 06 adds an optional **Reach station (right) / OFF** native toggle.
It connects the same accepted avatar Bird to the reusable Udon highlight and
spherical-scroll components in the same post-IK frame. Enter the wire sphere,
extend through its back and sweep to spin; withdraw to coast. Colors highlight
but do not select, and **RESET SPIN** uses native interaction. Clicking remains
disabled. The station starts off and does not change calibration or geometry
diagnostics. See [avatar UI timing and contracts](VRCHAT-AVATAR-UI.md) and the
latest checkpoint for build versus actual deployment status.

Lab 07 adds **ADAPTIVE** after the existing original **FILTERED** mode on the
Point comparison control. It tests responsiveness driven by incoming raw range
and correction of stale far-range history during return. RAW remains the saved
default. This revision is build-only; Lab 05 remains deployed for physical
geometry inspection. See [filter policy and limits](RANGE-ADAPTIVE-FILTER.md).

In lab v0.2, all 34 markers update every `PostLateUpdate`, after avatar IK.
Only text refresh is limited to 5 Hz. There is no marker smoothing, deliberate
delay or positional offset. The Lab 03 follow-up renders diagnostic joint/tip
dots through avatar skin; the original depth-tested dots could be hidden inside
the gray hands. Bird itself retains normal world occlusion. The original
lab sampled bone markers at 10 Hz and origin markers at 5 Hz; Dana correctly
noticed the resulting lag beside the smoothly animated avatar hands. Other
diagnostic scenes retain their opt-in/default cadence. See the official
[post-IK event](https://udonsharp.docs.vrchat.com/events/).

Dana's input preference is direct tracked hand-joint positions where a supported
platform API exposes them, with avatar bone positions as the acceptable fallback.
For the current VRChat world, the documented `GetTrackingData` API gives local
head/hand origins; finger positions from `GetBonePosition` belong to the avatar.
No supported raw finger-joint API has been established here. Never describe
these bone samples or inferred fingertips as exact real hand joints. Preserve
this source distinction when connecting Bird; avatar proportions and animation
can affect the fit even with a responsive per-frame sampler. See
[player positions](https://creators.vrchat.com/worlds/udon/players/player-positions/).

Native VRChat Interact controls toggle the mirror and markers. The mirror starts
off and is limited to 512-pixel resolution. Diagnostics are per-client; visitors
do not compete for the switches. Bird clicking is unnecessary. A named integration
root contains the separate input/solver/presentation station.

## Lab 12 sphere-center direction and fixed defaults

Dana approves the 45-degree knuckle-axis fallback but rejects locking the far
direction to it. The current policy retains sphere-center aim at every range;
only behind-palm centers blend to that fallback, and singular fits use it when
no direction exists. Scalar range stays independent. ADAPTIVE and PALM are the
saved defaults; both comparison buttons and labels are removed. Use
`-RefineSphereDirection` to author/update this scene. See
[PALM-DIRECTION.md](PALM-DIRECTION.md) and the latest CHECKPOINT for validation
and device state. Lab 13 subsequently removes required SET.

## Historical Lab 11 far knuckle-axis comparison

Lab 11 preserved Lab 10's PALM origin and rotated the far
limit 45 degrees toward the fingers about the index-to-pinky knuckle line.
Through four metres and for an unblended healthy fit, geometry is unchanged.
The range gate smoothly completes at 25 metres and preserves distance. Use
the historical Lab 11 artifacts for that comparison; its authoring switch and
policy have been retired. See [PALM-DIRECTION.md](PALM-DIRECTION.md) and the latest CHECKPOINT for
validation and device status. Dana confirms PALM feels much better and ADAPTIVE
seems good; this does not accept the barely-tried SPHERE mode. Lab 13 removes
mandatory SET while retaining manual correction.

## Lab 10 origin comparison

Lab 10 started with **Origin / PALM**, adding the pinky knuckle
to move the ray origin into the palm. Its native switch returned to CLASSIC
without repeating SET. The white cross marks the chosen origin; fitting and
the wrist/knuckle palm frame remain independent. Use `-AddRootControl` only
when adding the comparison to an older scene; it refuses to overwrite an
existing control. See PALM-DIRECTION.md and latest CHECKPOINT for tests and
headset status. Lab 10 is now hash-verified and visibly running in Quest VRChat,
with physical origin preference pending. The lab retains zero tilt for this comparison.

## Lab 09 palm direction comparison

The latest Quest deployment removes the authored flat-hand tilt, uses the
wrist/knuckles instead of the thumb to define the palm normal, and adds a white
cross at the unchanged reference ray origin. Dana reported the sideways motion
in RAW on both hands. See [PALM-DIRECTION.md](PALM-DIRECTION.md) for the comparison,
measurements and the later far-only tilt idea. The normal SDK transfer hash and
actual Lab 09 stereo rendering are verified; physical direction feedback is
pending. Calibrate both straight hands, inspect RAW with Geometry ON, then
compare SPHERE if useful. The Point cycle remains unchanged.

`-RefinePalmDirection` sets up this comparison on an older authored lab. Normal
reproduction uses the committed scene. If older layout/refinement switches are
used together, the runner applies this direction refinement last.

## Lab 08 sphere-filter comparison

The saved lab now has optional SPHERE following ADAPTIVE on the native Point
control. RAW stays the default; original FILTERED remains available. The new
policy filters the existing sphere-center vector's direction and length before
the original polynomial. See [SPHERE-FILTER-LAB.md](SPHERE-FILTER-LAB.md) for
contracts, limitations and validation. Lab 08 was deployed at Dana's request on
2026-09-27, with matching device hash and actual VRChat stereo rendering verified;
physical filter feel remains pending. The
`-AddSphereFilter` switch is only for an older scene without the new policy;
it refuses to overwrite one already present.

## Reproduce and test

From the lightweight repository, with the heavy BirdWorld project beside it:

```powershell
./tests/Invoke-UnityTrackingLab.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/BirdWorld' `
  -Check -CheckBird -Launch
```

Use `-Generate` only to create the absent scene: generation refuses to overwrite
an authored lab. Normal reruns restore maintained sources and stable GUIDs,
compile Udon and use the saved scene. Omit `-Launch` to build without contacting
a device. Omit `-Check` to skip the separate ClientSim/render check after a checked
scene change. Close other Unity editors using this project first.
`-SkipBuild` runs requested authoring/check steps without rebuilding the world;
it cannot be combined with `-Launch`. `-AddUi` adds the optional station only
when absent; `-RefineUi` explicitly updates that station's authored layout.

The runner uses the SDK's public Build / BuildAndTest methods. Android Build and
Test transfers a world bundle into the installed VRChat client; it does not build
another standalone APK. The Quest needs USB authorization, VRChat installed and
previously launched, and an awake/unlocked device. The first local-world launch
should start with VRChat closed. No online upload is needed. See
[official Android Build and Test](https://creators.vrchat.com/platforms/android/build-test-mobile/).

Inspect `lab-check-result.txt`, `lab-build-result.txt` and their logs in BirdWorld.
The preserved bundle and images go under ignored `Validation/TrackingLab`.
An SDK transfer/launch result is distinct from a successful in-client scene load.
The world logs `BIRD_TRACKING_LAB_READY` once its Udon status component has a local
player. Actual client logs and physical view must be assessed separately.

## Build the same lab for Windows and Android

The runner defaults to Android. Use `-Platform Windows` for PC, or build both
without launching either client:

```powershell
./tests/Invoke-UnityTrackingLab.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/BirdWorld' `
  -Platform Both
```

Add `-Check -CheckBird` after scene or runtime changes to run the saved-scene
and compiled-Udon avatar/UI suites on each selected target. Authoring switches
run only once. Both builds Windows first and Android second, leaving Android
selected for the usual Quest workflow. `-Launch` is explicit and accepts only
one platform; it uses that platform's ordinary SDK BuildAndTest. No mode uploads.

Each platform uses normal SDK scene/project validation, compilation/export and
compressed/uncompressed upload-size gates. An editor-only scene-processing
observer then checks the actual processed lab: one descriptor/pipeline and a
spawn, all authored Udon program bindings with nonempty compiled programs,
no missing scripts and no remaining project MonoBehaviours. It never alters
the scene or disables SDK validation. The normal SDK panel remains authoritative
for its warnings, account eligibility and online publishing.

The ignored `Validation/TrackingLab` folder contains separate
`BirdTrackingLab_Windows.vrcw` and `BirdTrackingLab_Android.vrcw`, each with a JSON
build record. The record includes the artifact hash/size, source scene identity,
Unity/Worlds SDK versions, maintained runtime/shader source hash, processed
component/program inventory and layout/network bindings. The final Both step
checks the bundle hashes and compares the two processed layouts, collider
shapes, spawn paths, component inventories, Udon source bindings and SDK network
IDs. Runtime sources must still match the records. Catalog loading confirms
the expected scene is in each bundle; it does not instantiate a VRChat client.

Results/logs in BirdWorld also retain their platform suffixes (for example,
`lab-build-StandaloneWindows64-result.txt`, `lab-build-Android-result.txt` and
`lab-platforms-result.txt`). Unsuffixed result/log files describe the most recent
step for compatibility with the existing workflow. Build records are evidence
for those artifacts, not an upload authorization or a guarantee of identical
platform rendering, performance or multiplayer behavior.

For a clean checkout, restore the pinned VPM packages with Creator Companion
or `vpm resolve project <absolute BirdWorld path>` before running the script.
The runner restores maintained runtime source/metas, shaders and lab helpers;
Unity regenerates Udon bytecode. It must not depend on another project's Library,
ignored compiled programs or old validation helpers. See the latest checkpoint
for the clean-restoration test and precise build evidence.

## Path to a private world

Open this same saved scene in the normal SDK panel, sign in to the uploading
VRChat account and choose online publishing when ready. Supply name, description,
thumbnail and capacity, and retain the assigned Pipeline Manager blueprint ID
for updates. Keep it private. The normal SDK Builder platform selector supports
Windows and Android together; both must use that same blueprint ID. The SDK
performs its ordinary build/upload for each platform from this authored scene.
The local audit bundles are validation artifacts, not a separate manual-upload
format. No lab-specific runtime conversion or replacement input system is needed.

Build and Test and upload share normal SDK scene validation/export. The helper
also calls the compressed and uncompressed world-size checks used by upload.
It preserves validation and uses unmodified SDK/client code. Runtime components
are standard world components and compiled Udon; editor fixtures are excluded
from the build. Existing demo scenes are preserved.

A local build cannot establish account upload eligibility, server acceptance or
future compatibility after changing the scene/SDK. Upload still requires New
User rank or higher and normal content metadata/agreement steps. Public listing
is unnecessary for inviting friends into a private world. These are the usual
publication steps, not a special migration for this lab. See
[first-world publishing](https://creators.vrchat.com/worlds/creating-your-first-world/)
and [cross-platform setup](https://creators.vrchat.com/platforms/android/cross-platform-setup/).

Next physically assess the experimental avatar-input adapter with one supported
avatar and the simple target. Keep tracked origins,
avatar bone positions, inferred endpoints, calibration and logical/presented Bird
points separately inspectable. Networked cursors and shared-object ownership are
independent later work.

## First device checkpoint, 2026-09-27 UTC

The Android world passed the SDK build and both upload-size checks. Bundle:
104,805 bytes; SHA256
`52728252A752C0B576DFA5228B96536CDF1F859F2143D6D1382E115A8BB05F9E`.
The normal SDK BuildAndTest API placed that bundle in the installed Quest
VRChat application's TestWorlds folder and launched the client. Once the headset
woke, an actual stereo device capture showed the authored room, avatar hands,
live bone markers, 16/16 bones for both hands, VR mode true, avatar eye height
1.60 m and updating tracking-origin/wrist separations. This establishes in-client
scene rendering and execution of both diagnostic Udon programs. It does not
establish Bird input fidelity, gesture feel, mirror usability or multiplayer.

The saved-scene ClientSim check also passes normal-frame diagnostics, both native
Interact events, marker disable/recovery, origin binding and spawn floor support.
Two editor camera captures were inspected; the capture camera excludes simulator
UI/player layers without dismissing the simulator's introduction or changing its
preferences. A preliminary Mobile/Diffuse tint issue was fixed with ordinary
Standard materials. Labels were enlarged. The long marker-button label still
clips its ON/OFF suffix; widen or fit that label in the next presentation pass.
The device was left running for Dana's inspection rather than reloaded for this
minor presentation change.

The Unity asset-bundle build still prints its internal "Build Finished, Result:
Failure." line even though the SDK reports success, size checks pass and the
actual VRChat client loads and runs this bundle. Its cause remains unclassified;
no validation was suppressed. Client startup also logs general UI/account/avatar
service errors; no lab Udon execution exception was found. Info-level world
heartbeat output was absent from the device log, so the actual stereo view and
live diagnostic text are the device evidence. Keep device logs/captures private.

SDK Android setup selected its mobile quality tier and changed audio voice counts
to 32 virtual / 24 real. Those normal project defaults are retained. No SDK source,
client, account settings, privacy permissions or persistent headset power setting
was modified. This world has not been uploaded online.

## Marker cadence correction, 2026-09-27 UTC

Lab v0.2 removes the marker sampling throttle after Dana's physical comparison
with the gray avatar hands. The compiled-Udon ClientSim check now perturbs all
34 marker transforms between frames, then requires each to match its current
SDK position after the next post-IK update on 30 consecutive frames. Both native
controls, marker disable/recovery and spawn support still pass. The marker-toggle
ON/OFF text now fits, verified in editor renders and an actual Quest stereo capture.

The normal SDK Android BuildAndTest workflow rebuilt and transferred 105,951
bytes; device SHA256 matches
`9A2FBB28FC500998AA07193ABB5DDBBFCE957D6C7C9BD4149B7B83C0A3C74A8D`.
The updated lab is visibly running inside VRChat. This proves deployment and
runtime operation; Dana subsequently confirmed that the markers look good.
The prior SDK internal failure-log qualification remains. No matched Udon runtime
execution exception was found in the client log. The headset had slept; one
ordinary wake command sufficed, without persistent power-setting changes.
