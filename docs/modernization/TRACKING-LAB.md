# VRChat tracking laboratory

`BirdWorld/Assets/BirdWorld/Scenes/BirdTrackingLab.unity` is the first-client
milestone: a normal authored VRChat world for inspecting avatar hands before
connecting Bird. It is separate from the standalone OpenXR comparison and the
desktop-input interaction demonstration scenes.

The world provides a protected floor/spawn, meter grid, 10/25/50 cm objects,
distant range references, a work bench, a switchable SDK mirror and local hand
diagnostics. Cyan/pink spheres show avatar bone origins; amber cubes show the
SDK's left/right TrackingData origins. Bone counts indicate availability, not
tracking confidence. Distal bone origins are not fingertip endpoints. The console
reports avatar eye height and hand-origin/wrist separation. Nothing fits a sphere,
calibrates the avatar, records hand data or modifies the player.

Native VRChat Interact controls toggle the mirror and markers. The mirror starts
off and is limited to 512-pixel resolution. Diagnostics are per-client; visitors
do not compete for the switches. Bird clicking is unnecessary. A named integration
root reserves space for a separate input/solver/presentation station.

## Reproduce and test

From the lightweight repository, with the heavy BirdWorld project beside it:

```powershell
./tests/Invoke-UnityTrackingLab.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/BirdWorld' `
  -Check -Launch
```

Use `-Generate` only to create the absent scene: generation refuses to overwrite
an authored lab. Normal reruns restore maintained sources and stable GUIDs,
compile Udon and use the saved scene. Omit `-Launch` to build without contacting
a device. Omit `-Check` to skip the separate ClientSim/render check after a checked
scene change. Close other Unity editors using this project first.

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

## Path to a private world

Open this same saved scene in the normal SDK panel, sign in to the uploading
VRChat account and choose online publishing when ready. Supply name, description,
thumbnail and capacity, and retain the assigned Pipeline Manager blueprint ID
for updates. Keep it private. Add a Windows build under that same ID if PC friends
should join; the Android version serves Quest. No lab-specific runtime conversion
or replacement input system is needed.

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

Next connect the tested Bird solver to a characterized avatar-input adapter,
starting with one supported avatar and one simple target. Keep tracked origins,
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
