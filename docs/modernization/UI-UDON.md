# Local Bird UI in VRChat Udon

The maintained VRChat integration now has Udon counterparts for logical input,
menu routing/focus, actions and back-surface spherical scrolling. The heavy
repository contains an authored `BirdWorld/Assets/BirdWorld/Scenes/BirdUiDemo.unity`
station. Reusable runtime and its generator/checks remain in the light repository.
The ordinary Unity UI assembly is still independent of the Worlds SDK.

This station demonstrates local interactions: reach through an opening control,
click through Colors, choose among twelve colors, flick the larger sphere from
its back surface, return with Back, close the menu, or reset its rotation.
The color result is a separate renderer. Dodecahedral layout and wire guides are
baked scene objects; neither selection nor scrolling depends on their appearance.
All seven Udon programs use `BehaviourSyncMode.None`; this is not a networked
shared selector. The point, sphere fit, range, Kalman and click laws are unchanged.

## Components and authoring

| Component | Responsibility |
| --- | --- |
| `BirdUiPointer` | Accepted logical samples, validity, press edges, history and user grouping |
| `BirdUiPanel` | Separate content object, explicit parent, open/background/closed focus |
| `BirdUiElement` | Collider policy, priority, visual state and local Udon action/state callbacks |
| `BirdUiRouter` | Registered controls and inputs; evaluate candidates before dispatching actions |
| `BirdUiSphericalScroll` | Far-surface contact, one driving hand, rotation, inertia and cancellation |
| `BirdUiAction` | Open, Back, Close Root, Set Color and Reset Transform scene actions |
| `BirdUiDesktopInput` | Explicit desktop demonstration: head direction, wheel range, mouse press |

Activation and action choices are Inspector enums. Callbacks use a serialized
UdonBehaviour target and named public custom event, matching the SDK's normal
local event workflow. An action can read `BirdUiElement.lastPointer`; a state
listener can read `state` (Inactive=0, Enabled=1, Highlighted=2, Activated=3,
Background=4). Optional color feedback uses a renderer property block; it does
not change hit geometry or shared materials. General transform/color animation
authoring and map controls remain later UI work.

Keep panel controllers and the opening gate outside the content they hide.
Register inactive child controls too, and assign one router to each control.
Configure the arrays before sampling, then call `Initialize` after deliberate
between-frame registry changes. Higher priority wins; equal priorities choose
the nearest target, with registry order breaking exact ties. The supported
volumes are Box, Sphere, Capsule and convex Mesh colliders. A finite segment
defines point-through; directional passage uses the point's swept path.

Both hands use distinct pointers with the same `userId`. This groups focus on
one local client; it is not network ownership or authentication. Another local
group cannot steal an open branch. One tracked hand retains focus; losing both
closes the root by default. Back restores the parent. Cyclic parent assignments
are rejected. Callbacks may close branches: dispatch rechecks eligibility so a
closed branch cannot receive a second hand's already-collected action.

## Input and update contract

An input producer sets `sampleOrigin`, `samplePosition`, `sampleTracked` and
`samplePressed`, then calls `Submit()` once per sample. Position is the logical
Bird point, never a distant marker's projected transform. A first/recovered held
press establishes a baseline and cannot click. Invalid data, user changes and
disable cancel input. Producers must cancel when they stop; there is no timeout
or queue, so multiple samples before one route pass can replace an edge.

For an existing accepted `BirdCursorState`, `SampleCursor()` copies its current
state without stepping the solver. Assigning `cursor` polls that state in
LateUpdate at order 50. Explicit producers can instead leave it empty and call
Submit after their solver sample. The router runs at order 100 and scrolling at
110. The poller does not recover intermediate transitions hidden between polls.
The experimental avatar-bone adapter still suppresses clicks and has unresolved
physical calibration; this UI work does not change that limitation.

The desktop scene uses a separate, clearly labeled synthetic input producer.
It is inactive for VR users. Its 0.1–30 m wheel range is a desktop control choice,
not a Bird geometry or UI contact limit. No headset deployment or hand-tracking
claim follows from this station. Disable that producer before supplying another
source to the same pointer.

The outer sphere must be independent from its rotating content and larger than
the selectable layout. Front entry and the entire interior remain free of scroll
drive. Only passing the far surface plus the 3 mm entry margin acquires; crossing
back inside releases into coasting. No press is required. Defaults retain the
Unity implementation's 10/s input response, 0.99/s damping and 720 degrees/s
input cap. Loss, disable, owner change, closed/background panel, invalid sphere
or a step over 250 ms clears inertia. Different-hand acquisition rebases without
inheriting an impulse. Parameters are experience choices, not geometric Bird laws.

## Reproduction

From the light repository, with the heavy BirdWorld checkout and its installed
Worlds SDK 3.10.5:

```powershell
./tests/Invoke-UnityUdonUiChecks.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/BirdWorld' -BuildWorld -BuildAndroidWorld
```

The runner restores source/meta pairs into ignored `Assets/BirdGenerated`,
compiles Udon, reopens the saved station, and exercises backing Udon heaps/events
under ClientSim. It never calls runtime proxy methods. `-Generate` creates a
missing station and refuses to overwrite an existing scene. Result files are
reset before each process and timeouts enforced. The optional build steps use
the SDK's public build-only API and an ignored scene copy; it neither uploads nor
launches VRChat. Generated captures and bundles stay under ignored `Validation`. Android setup
lets the SDK and UdonSharp install their own active-platform defines and waits
for compilation to settle before a separate build process. The last requested
platform remains selected in the editor; subsequent checks explicitly select Windows.

The SDK-specific adaptation uses exposed `Input.GetAxisRaw("Mouse ScrollWheel")`
and explicit collider type checks. Direct VM fixtures write enum values as their
integer representation; the Inspector and Udon C# retain typed enums.

## Measured checkpoint, 2026-09-26

Unity 2022.3.22f1 / Worlds SDK 3.10.5 passes **441 compiled-Udon assertions**,
including a normal-LateUpdate scene sequence and four inspected D3D11 captures.
The [rate measurements](measurements/udon-spherical-scroll-rates.csv) cover
30/72/120 Hz in identity, upside-down and rotated reference frames. The largest
coast-angle deviation from the analytic 84.28226 degrees is 0.00009 degrees.
These use synthetic sample intervals; they do not measure device frame pacing.

The public SDK build API completed and produced these fresh files under the
heavy repository's ignored `Validation/WorldBuild`:

| Target | File | Bytes | SHA256 |
| --- | --- | ---: | --- |
| Windows x64 | BirdUiDemo.vrcw | 183768 | `91454954F5DABDEC83EEA225229F54036C024F07052A6BCCB780532089EC7D38` |
| Android | BirdUiDemo_Android.vrcw | 153521 | `75F28B46AB52272BD84658E3432D068A0D33CC5036FC7E851990B4E43C566B11` |

The independent `UnityWorldBundleChecks.AuditUiBundles` editor entry point loads
both files with `AssetBundle.LoadFromFile` and verifies their expected scene
catalog. This passes, without instantiating either scene. An attempted CRC audit
was unsuitable: `GetCRCForAssetBundle` reads a `.manifest` sidecar, which these
copied SDK outputs do not include ([Unity API documentation](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/buildpipeline/getcrcforassetbundle)).

Both build logs nevertheless contain Unity's internal scene-bundle message
`Build Finished, Result: Failure.` despite SDK completion, fresh artifacts and
successful catalog loading. No corresponding final compilation error was found;
the reason remains unresolved. Treat these as SDK-produced, catalog-checked
artifacts, not a clean client validation. Android project settings were configured
by the installed SDK (including ARM64, API 25 minimum / 33 target and defines).

Compiled execution, editor rendering and bundle catalog loading remain separate
from VRChat-client, Android runtime,
multiplayer, physical hand input and subjective legacy-feel validation. The
station is one world-development slice; the original adoption flow, mandala,
paired tabletop/building Hanoi and complete world arrangement remain planned.

SDK references: [UdonSharp language and component model](https://creators.vrchat.com/worlds/udon/udonsharp/)
and [editor proxies and backing behaviours](https://udonsharp.docs.vrchat.com/editor-scripting/).
