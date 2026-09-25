# Development checkpoint

## Initial pass

- Both repositories cloned; feature/vrchat-modernization pushed in each.
- Initial request preserved; implementation plan and platform gates recorded.
- Fixed OpenXR pose-read compilation/failure handling and stopped-subsystem reuse.
- 36 contract checks pass. These use API doubles, not Unity or physical tracking.
- Git diff whitespace check passed; package branch local HEAD matched its upstream after push.
- Headset detected by ADB but unauthorized. User must accept USB debugging in the headset before deployment/inspection.
- Both demo projects are Unity 2020.3.33f1. Required VRChat editor installation and real compilation remain pending.

## Recurring task

App automation ID: `develop-bird-for-vrchat`. Heartbeat targets this existing conversation. Normal cadence: every two hours, one small development checkpoint per run. Saved approvals include gh repo operations and `git push -u origin feature/vrchat-modernization`. The automation API does not expose a separate permission-profile setting; unattended inheritance must be verified by the actual scheduled run.

For the initial scheduler test, temporarily use a two-minute interval. The first triggered pass MUST restore the normal two-hour cadence through automation_update before doing any work, retaining the existing name, destination, target, status and normal prompt. The normal prompt is stored in `RECURRING-PROMPT.txt`. Then verify repo read/write, execute the contract checks, update this checkpoint with actual scheduled-run evidence, commit and push. Do not claim the scheduler test passed based only on the manual initial pass. If unable to restore cadence, pause the automation and report the failure instead of running development every two minutes.

## Actual scheduled smoke test â€” 2026-09-22

The app triggered this conversation at 06:45:33 UTC. Before testing, automation_update restored the normal prompt and two-hour cadence. The saved configuration confirms ACTIVE, heartbeat, and the same target conversation.

This scheduled run received `danger-full-access` filesystem/network access and approval policy `never`, matching the current conversation environment. This is broader than the restricted environment used during the initial manual preflight; no permission settings were changed by the agent. The automation API has no separate permission-profile field.

- Repository read/write: passed; initial working tree was clean on feature/vrchat-modernization.
- Test execution: passed, 36 OpenXR contract checks using API doubles. No Unity, Udon, or headset validation is implied.
- Commit/push: passed without approval prompts. Commit `68d4bcd` was pushed successfully to origin/feature/vrchat-modernization by this scheduled run.
- Scheduler smoke test: passed. The normal two-hour cadence is restored and active.

Computer must remain on with the app running for local scheduled work. The initial smoke test is a one-time step; subsequent scheduled runs should follow PLAN.md rather than repeat a checkpoint-only test.

## User steering â€” 2026-09-22

Headset setup is explicitly deferred until the user says it is ready. Do not poll the device or repeat setup requests. Continue all independent implementation and desktop/editor validation. Keep real tracking, usability and device performance testing deferred and clearly labeled; the headset must not block progress on the rest of the project. The recurring prompt and plan reflect this priority.

## Scheduled development pass â€” 2026-09-22, 08:45 UTC

Fixed stale click edges and held selection when tracking disappears. Bird now resets per-update press/release edges before checking tracking, emits one release if selection was held, and clears stale movement by setting the previous position to the held cursor position. Normal tracked sphere fitting and click thresholds are unchanged. This uses the existing click-up API, so consumers that distinguish cancellation from user release will need an explicit future cancellation API.

Added tests/Invoke-UnityCoreChecks.ps1 and UnityCoreChecks.cs. They copy the current production Hand/Bird/Kalman sources into a generated project under the heavyweight repo's ignored Validation directory and run inside the installed Unity editor. No maintained source copies, Library contents, or bulky assets are committed. Initial harness creation pulled default Unity packages; adding ProjectSettings/ProjectVersion.txt before launching prevents this new-project initialization on future runs.

Validation evidence:

- Before the fix, actual Unity 2020.3.33f1 compilation/execution reached the expected failure: `Tracking loss must clear the prior press edge` (exit 1).
- After the fix, the same runner passed all 15 core checks (exit 0): known sphere center/radius, index penetration, startup without tracking, loss on press/release, continued loss, and recovery.
- Existing 36 OpenXR API-double checks still pass. Git whitespace check passed.
- This is real Unity core execution with synthetic input, not full package import, XR Hands API compilation, Udon validation, rendering or physical-device validation. The headset was not accessed.

Next bounded work: degenerate/nonfinite sphere input and recovery motion tests, then actual XR Hands/package compilation on the supported modern editor. Separately observed during inspection: KalmanFilterVector3.Update(List<Vector3>) advances its index before reading and needs a regression-backed repair. Leave that independent fix for a later checkpoint.

## Scheduled development pass â€” 2026-09-22, 10:46 UTC

Repaired the batch Kalman-filter overload identified above. It previously advanced the index before reading, skipping the first chronological measurement and eventually throwing out of range for every nonempty list in either ordering. It now consumes each sample before advancing. The scalar filter equations and empty-batch behavior are unchanged. This repairs the public batch API; the existing Bird cursor uses the scalar overload.

Actual Unity 2020.3.33f1 regression run before the fix failed with ArgumentOutOfRangeException from KalmanFilterVector3.Update(List<Vector3>). After the fix, all 33 core checks passed (15 existing plus 18 batch checks): single/multiple samples, both list orderings, default/overridden noise, subsequent filter state and empty input. Git whitespace check passed. No full package, modern editor, XR Hands, VRChat or physical-device validation was performed in this checkpoint. No headset access.

Next: resume degenerate/nonfinite sphere input and recovery-motion validation, followed by modern editor/package setup. The batch-filter issue is resolved.

## Scheduled development pass â€” 2026-09-22, 12:47 UTC

Added validation before committing a hand pose to Bird's public geometry or smoothing filter. Reject nonfinite root/index-tip/fitting-joint inputs, nonfinite sphere results, nonpositive radius or pointing distance, and nonfinite range-mapped measurements/noise. Rejection holds the previous valid cursor/geometry and uses the same one-time release behavior as tracking loss. The valid-pose mapping and filter equations are unchanged.

Actual Unity 2020.3.33f1 regression evidence: before the fix, a NaN fitting coordinate failed `Invalid pose must hold cursor position; mode=0`. After the fix, all 61 checks passed: the previous 33 plus 28 assertions across NaN fitting input, positive-infinite index tip, negative-infinite thumb base, and all-zero hand input. Recovery is compared against an independently advanced valid-input Bird instance to verify that rejected frames do not poison its filter. Whitespace check passed. No headset access; full package/XR Hands/Udon validation remains pending.

Limits: this is not a complete conditioning policy for coplanar/nearly singular point clouds; finite zero sentinels for individual missing joints still need an explicit adapter validity contract. Changed-pose reacquisition motion is also untested. Next pass should prioritize modern Unity setup and package integration rather than indefinitely extending isolated core tests.

## Scheduled development pass â€” 2026-09-22, 14:47 UTC

Extended Invoke-UnityCoreChecks.ps1 with `-AllSources`, using a separate generated project and validation-mode marker. It copies all 11 production source files into Assets and compiles the provider, manager, interactable and configuration window as well as the core. Optional tracking adapter bodies remain excluded without their defines. The first run exposed a missing IMGUI module in the minimal harness; adding explicit physics and IMGUI modules resolved it. Unity 2020.3.33f1 all-source compilation and all 61 core checks passed. This is not a UPM install or a player build.

Rechecked VRChat's supported editor: 2022.3.22f1. Unity's official release page supplies changeset 887be4894c44. Attempted installation through the existing Unity Hub headless CLI. Hub's release/config requests returned 404, then its elevation request was declined/dismissed and Hub logged cancellation. No 2022 editor was installed. Do not automatically retry elevation or work around the dismissal; leave editor setup deferred while independent package work continues. Logs are in the local temporary directory as bird-unity2022-install.txt and bird-unity2022-install-error.txt, with the cancellation in the local UnityHub info-log.json. No headset access.

Package audit: package.json unconditionally depends on Ultraleap 6.8.1 despite backend compile guards; there are no tracked asmdefs, and .gitignore excludes asmdefs/meta files. A real UPM import path needs optional backend assembly boundaries and stable metadata, not just copying scripts. Next pass should implement/test the core package boundary on the available editor, keeping modern XR Hands and VRChat validation explicitly pending.

Sources: https://creators.vrchat.com/sdk/upgrade/current-unity-version/ and https://unity.com/releases/editor/whats-new/2022.3.22f1 .

## Scheduled development pass â€” 2026-09-22, 16:47 UTC

Established a baseline UPM package: Bird3D.Runtime and Editor-only Bird3D.Editor assembly definitions; moved the configuration window into Editor; tracked stable metadata instead of ignoring all .meta/.asmdef assets; replaced the unconditional Ultraleap dependency with the built-in physics and IMGUI modules. Added package installation/migration documentation and the package-local license. Existing guarded tracking sources are retained, but SDK assembly reference setup is not yet automated or validated. No serialized GUID migration from the legacy unitypackage is claimed.

Added `-Package` to the validation runner. A fresh generated Unity 2020.3.33f1 project resolved the actual local UPM dependency and executed 63 checks successfully (61 core checks plus runtime/editor assembly checks), with zero copied production scripts in Assets. No Ultraleap SDK or adapter symbol was needed. The existing all-source mode now accounts for the relocated editor script and removes only its obsolete generated copy in marked validation projects.

Next: player-build validation of editor/runtime isolation, then optional adapter assembly boundaries and explicit backend registration. Backend-enabled compilation, modern Unity and VRChat validation remain pending. Do not repeat the declined editor elevation request or access the deferred headset.

## Scheduled development pass â€” 2026-09-22, 18:47 UTC

Added `-Package -BuildPlayer` to the generated-project runner, plus editor build and runtime smoke-test scripts kept outside the distributed package. This separate mode builds a Windows x64 Mono player, requires Bird3D.Runtime.dll in its output and rejects Bird3D.Editor.dll, then launches the player headlessly and checks a fresh result and exit code. The runtime smoke test instantiates an untracked Bird, checks idle state/initial position and verifies no editor assemblies are loaded.

Actual Unity 2020.3.33f1 result: player build PASS, standalone startup/runtime smoke test PASS, both exit 0. Production code came from the UPM package, with no copied production scripts. The build used StrictMode and all generated artifacts remain under the heavyweight repo's ignored Validation/PackagePlayer2020 directory. Git whitespace check passed. The existing 63 editor checks were not rerun because this checkpoint changes only the validation tooling and adds a distinct player-build gate.

Limits: Windows Mono startup only; no rendered interaction scene, provider/interactable lifecycle, IL2CPP, Android, backend-enabled hand tracking or VRChat verification. This older-editor build is local validation output, not a distributable release. No headset access or editor-install retry. Next: optional backend assembly separation/registration and SDK integration.

## Scheduled development pass — 2026-09-22, 22:47 UTC

Removed concrete SDK constructor references from HandFactory. Backends register constructor delegates; callers can query availability and unregister, and missing registrations/null returned hands give explicit errors. Existing Leap/OVR/XR Hands adapters register before scene startup and expose a public registration method for edit-mode callers. Registration is cleared at Unity's SubsystemRegistration phase to avoid retaining callbacks between play sessions. Existing conditional enum names/values are unchanged. This is preparation for optional assemblies, not their completed separation.

Validation in actual Unity 2020.3.33f1: local UPM import and all 75 editor checks passed (63 prior plus 12 factory assertions). A Windows x64 Mono player rebuilt successfully and passed startup through a synthetic backend registered by Unity's real BeforeSceneLoad callback. Runtime/editor isolation still passed. All 36 OpenXR API-double checks passed, now constructing both hands via explicit adapter registration and the factory. Whitespace check passed. No headset access or editor-install retry.

Limits: no real SDK-enabled Unity compile, GUI/visual test, or VRChat validation. Domain-reload-disabled repeated Play Mode entry is handled by the reset hook but not yet exercised. Edit-mode factory callers now explicitly register an adapter first; this behavior and initialization order are documented in the package README. The conditional enum's serialized-value sensitivity remains unresolved and must be considered before backend selection changes. Next checkpoint: optional SDK assembly separation or a synthetic visual scene; keep the scope bounded.

## Scheduled development pass — 2026-09-23, 00:47 UTC

Added an optional Desktop Preview UPM sample. It creates two colored cursors, synthetic sphere-fit points, index-tip markers and root-to-cursor lines, driven by two production Bird solvers. IMGUI controls vary radius, animate orientation, simulate index penetration and toggle pose availability. Press/release counters expose both hands' click edges. Input is explicitly labeled non-anatomical synthetic data, with no hardware access. The component and generator source live in the lightweight repo; the generated scene is in the heavyweight repo's ignored Validation/Preview2020 project.

Added `-Package -Preview` to the runner. Actual Unity 2020.3.33f1 imported the package, compiled the sample, saved Assets/BirdDesktopPreview.unity and exited successfully. This gate verifies compilation/scene generation only, not Play Mode behavior or the existing 75 editor assertions. The preview targets the built-in render pipeline; other pipelines and standalone shader inclusion are untested.

Attempted GUI inspection using the computer-use skill. Unity loaded the generated project, but no targetable Unity window appeared in the desktop helper's window list; the helper's explicit launch attempt returned `launched app did not expose a targetable window`. No GUI clicks or Play Mode inspection occurred. Closed only the background Unity process created for this attempt. Do not call this preview visually or interactively validated. No headset access, SDK install or elevation retry.

Next bounded checkpoint: get the generated preview into Play Mode and verify visuals, radius changes, selection and one-time release on lost pose. Optional backend assembly separation and SDK-enabled compilation remain pending. Existing core tests were not repeated because production solver/factory code did not change.

## Scheduled development pass — 2026-09-23, 02:48 UTC

Added automated Play Mode integration checks for the Desktop Preview, invoked with `-Package -Preview -PlayMode`. The generator saves the interactive scene first, then creates a separate check scene. A generated-project-only runtime component drives the sample's controls and observes real Bird solvers and Unity objects across frames. Fresh result/exit verification and the runner's three-minute timeout remain in force. These tests are not shipped inside the package sample.

The first actual Unity 2020.3.33f1 run verified cursor/radius/selection/loss/recovery behavior but failed cleanup: removing the component left its camera and marker geometry alive. Fixed ownership by placing generated objects beneath a dedicated root and destroying only that root plus the owned materials. Unrelated authored children are preserved.

After the fix, all 58 Play Mode checks passed (exit 0): known sphere fitting, two separate cursors, cursor/ray synchronization, opening-induced range increase, selection size and single press edges, held positions and exactly one release per hand on pose loss, input-marker visibility, unpressed recovery, collider removal, and cleanup of camera/geometry/materials without deleting unrelated children. Whitespace check passed. Production solver/factory code was unchanged; the separate 75 editor assertions were not rerun.

Limits: headless Play Mode, not rendered visual review or actual GUI input events. No SDK-enabled tracking, VRChat or physical-device validation. No headset access, GUI launch retry or editor-install attempt. Next: visual review or optional backend assembly separation, with platform and modern-editor gates still explicit.

## Scheduled development pass — 2026-09-23, 04:49 UTC

Added optional camera-render validation via `-Package -Preview -PlayMode -RenderPreview`. It enables graphics in the batch editor, requires a non-null graphics device, captures the sample camera to 1280x720 RenderTextures, and checks in-frame expected-color pixels near each cursor in idle, selected and lost-pose states. PNG encoding uses the image-conversion module only in the generated validation project. The runner removes its three previous capture files before a render run so stale images cannot be mistaken for fresh output. Generated images stay in the heavyweight repo's ignored Validation/Preview2020 directory.

Actual Unity 2020.3.33f1 ran with Direct3D 11 on the local NVIDIA renderer and passed 73 checks: the 58 existing Play Mode assertions plus 15 renderer/framing/pixel checks. Inspected all three PNGs: cursor colors and input-marker visibility match the state, but initial framing was too wide. Reduced the sample camera field of view from 60 to 30 degrees. The rerun passed all 73 checks; all three new captures were visually inspected and show larger, distinct cursors/point clouds without clipping at the tested poses. Whitespace check passed.

Limits: these are camera renders, not Unity GUI screenshots. IMGUI layout/input, full animated-motion framing, non-16:9 views, other pipelines/platforms, standalone shader inclusion, VRChat and hardware remain unvalidated. No headset access, GUI-helper retry or editor installation. Next work should advance optional SDK packaging or interaction/trails implementation rather than indefinitely expanding validation tooling.

## Scheduled development pass — 2026-09-23, 06:49 UTC

Added reusable BirdTrail runtime helper and two short trails to Desktop Preview. Fixed-capacity arrays bound retained points (default 128, allowed 2–4096); default lifetime 2.5 seconds, minimum interval 20 ms and minimum movement 3 mm constrain sampling. Expired points are evicted. Endpoint age controls alpha, interpolated along a caller-owned LineRenderer. The sample owns a shared transparent Sprites/Default material and exposes Clear trails. Loss of tracking, invalid input and clock rewind clear strokes; recovery starts from one new anchor, preventing a cross-gap connecting line.

Actual Unity 2020.3.33f1 Direct3D 11 run passed all 94 Play Mode/render checks, adding 21 to the previous 73. New checks cover capacity/ring order, stationary fade/expiry, thresholds, clear, invalid points, rewind, loss/recovery, sample integration and material cleanup. Inspected preview-trails.png: distinct cyan/pink curved trails fade toward their tails and remain attached to their respective cursors. Generated image remains in the heavyweight repo's ignored Validation/Preview2020 folder. Whitespace check passed.

Limits: fading interpolates endpoint ages rather than exact per-vertex ages. Fixed managed storage is established by implementation, but no device allocation/render profiling is claimed. Teleports while tracking require the caller to Clear. GUI button input, alternative pipelines, standalone shader inclusion, Udon/networking, mandala persistence and hardware remain unvalidated. No headset access or editor-install retry. Next prioritize SDK packaging or interaction work; do not mark the complete expression/trails stage finished yet.

## Scheduled development pass — 2026-09-23, 08:50 UTC

Repaired BirdInteractable event lifecycle. Drag previously invoked OnDeselect on every idle frame. MotionType.None also discarded activation state every frame, breaking balanced event-only drag/toggle behavior. Deselection now requires an active selection, and event-only targets retain that state. Disable/provider loss ends selection once, resets hover state and restores a snap collider when applicable. Touch can begin a fresh hover selection after re-enable; drag waits for a fresh press.

Added 12 actual component assertions to the preview Play Mode suite. The first regression run failed `Idle interactable must not emit deselection` against the original code. After repair, all 85 headless Play Mode checks passed in Unity 2020.3.33f1: existing preview/trail checks plus idle/held/release, tracking-loss release, disable/re-enable, provider loss, event-only toggle and touch entry/exit. The touch test disables before changing modes and explicitly synchronizes collider transforms. Whitespace check passed. Rendering was not rerun because visual code did not change.

Validation uses the real BirdInteractable and real synthetic Bird via a disabled BirdProvider with its private solver injected. It does not establish SDK-backed provider startup, all motion/orientation modes, runtime mode switching, or a complete interaction scene. OnDeselect remains a release/cleanup signal, including cancellation-like cases, not proof of deliberate activation. Existing provider/manager setup issues and broader lifecycle audit remain. No headset access, GUI automation or editor-install attempt.

## Scheduled development pass — 2026-09-23, 10:50 UTC

Fixed BirdManager detector destruction when trigger events have no subscribers, using null-safe invocation snapshots. Register/unregister are now idempotent and notify after changing membership, so event handlers see current list state. Missing BirdDetectorLayer is checked before allocating an object. DestroyUnusedBirdDetectors no longer inspects Exit twice or guesses ownership from delegate Target: since these events are global, it conservatively retains detectors while either event has any subscriber.

Actual Unity 2020.3.33f1 regression first reproduced NullReferenceException in DestroyBirdDetector with neither event subscribed. After the fix, all 94 headless Play Mode assertions passed (85 prior plus nine manager checks), covering empty subscriber cleanup, duplicate registration, callback membership, missing-layer partial-object prevention, enter-only and exit-only listener retention, unused destruction and idempotent removal. Whitespace check passed. This is distinct from the earlier 94-check rendering run; rendering was not rerun for lifecycle changes.

Limits: tests use a disabled synthetic provider and detector fixture objects with the expected names, not real trigger physics or SDK-backed provider startup. Name collisions, detector placement/radius and static-state lifetime remain audit items. Provider.Start also initializes associatedUser after registration and needs a future startup-focused pass. No headset access, GUI automation or editor-install attempt. Next prioritize optional SDK boundaries or an actual provider startup/interaction scene, keeping changes bounded.

## Scheduled development pass — 2026-09-23, 12:50 UTC

Exercised full BirdProvider startup and teardown with a registered synthetic backend and real prefab clones, instead of injecting a prebuilt solver. Fixed initialization order so default/custom user identity and chirality are ready before OnBirdCreated; a pre-start custom user ID is preserved. Backend construction precedes visual allocation. Added private ownership references and finally-protected cleanup for the generated cursor, sphere, 16 fitting markers and hit marker; caller-owned templates/materials are retained even if public marker fields are reassigned.

Actual Unity 2020.3.33f1 regression first failed `Provider registration callback must see initialized identity and solver; hand=0`. After repair, all 112 headless Play Mode assertions passed (94 prior plus 18 startup/destruction assertions across both hands). Verified factory chirality, identity/query visibility during callbacks, solver construction, marker updates, hidden debug geometry and destruction of every clone while preserving the source/owner. Whitespace check passed. Rendering was not rerun for this lifecycle change.

Limits: backend input is synthetic, so enabled SDK startup/hand fidelity remain unverified. Invalid prefab configuration, disabled-but-not-destroyed provider behavior, debug toggle updates and real detector physics are not established by this test. No headset access, GUI automation or editor installation. Next prioritize optional adapter assembly boundaries or user-facing scene composition rather than adding more isolated validation modes.

## User-triggered development cycle - 2026-09-23

Added BirdInteractableVisual: a composable color/scale feedback component driven by new read-only IsHovered/IsSelected properties. Selection takes precedence over hover; transitions use a small owned smoothstep interpolation with unscaled time. It changes a dedicated visual renderer's property block and local scale, preserving shared materials and restoring captured state on disable. No DOTween or other dependency was added.

Actual Unity 2020.3.33f1 Play Mode plus Direct3D 11 rendering passed 146 assertions, including eight feedback behavior assertions and five extra camera checks. The first camera run found the test cube occluding the cyan cursor; moved the visual test fixture while retaining its separate hit collider, reran successfully and inspected preview-feedback.png. The selected visual is orange and both cursors remain visible. Existing synthetic provider, manager, interaction and trail checks also ran. Whitespace check passed. This is still synthetic input, not a VRChat world or real hand-tracking validation.

Recorded the user's successful Quest 3 USB-debugging authorization (ADB device state, model and Android 14; no serial identifiers recorded). Updated the existing ACTIVE heartbeat prompt and its repo mirror to allow relevant device checks; the two-hour cadence is unchanged. No headset app was installed or launched during this cycle. Modern editor installation still requires resolving the previously declined elevation separately.

Limits: feedback currently expects `_Color` shader support and exclusive ownership of its renderer property block/visual scale. GUI input/layout, alternative render pipelines and full interaction-scene composition are unverified. Next prioritize SDK packaging or scene integration rather than expanding this validation harness indefinitely.


## XR Hands assembly boundary - 2026-09-23

Moved OpenXRHand.cs and its existing GUID into Runtime/XRHands with a separate Bird3D.XRHands assembly. BIRD_OPENXR_ENABLED gates compilation; references are Bird3D.Runtime and Unity.XR.Hands. Confirmed the SDK assembly name from Unity's registry archive for com.unity.xr.hands 1.3.0, which requires Unity 2021.3. No SDK was installed in the validation projects. Leap and Oculus remain guarded in the core assembly; conditional enum serialization is unchanged.

Validation on installed Unity 2020.3.33f1: 75 package checks, 73 all-source checks, successful Windows x64 Mono build and backend-registration/startup smoke run. The relocated adapter passes 36 API-double checks. Existing script GUID preserved. These verify the SDK-absent configuration and adapter contracts, not real SDK assembly linking or tracking. No headset polling or installer elevation attempted. Next: supported-editor SDK-enabled compilation, remaining adapter boundaries, or richer desktop interaction integration while modern-editor setup remains unavailable.


## Radial mandala desktop checkpoint - 2026-09-23, 16:53 UTC cycle

Added reusable BirdRadialTrail, composing the bounded BirdTrail with caller-owned renderers around a fixed world-space center/axis. Supports 1-12 supplied renderers, live multiplicity changes that clear old strokes, and all-copy clearing/tracking-loss behavior. Desktop Preview preallocates eight copies per hand, exposes a 1-8 slider and starts with ordinary one-copy trails. Maximum preview trail history is 2,048 points; no growing stroke collections or additional materials per copy.

Validation: actual Unity 2020.3.33f1 Play Mode with D3D11 rendering passed 163 checks. New checks cover translated-origin rotations, per-copy budgets, multiplicity preservation/reset, inactive copies, tracking loss, invalid input, clear and invalid configuration. Inspected the six-copy mandala camera capture: cyan/pink radial strokes are visible and contained in the view. GUI input/layout, device performance and real hand tracking remain unvalidated. No headset polling or installation attempt. Next integrate Bird-driven interactions into the user-facing desktop scene or continue optional SDK packaging; modern editor/VRChat and adoption-platform gates remain open.


## Quest ARM64 build and USB deployment - 2026-09-23

User asked to prioritize headset setup while present, then chose build/install now and visual testing later. Quest 3 remained USB-authorized. Created dedicated ignored Validation/QuestSmoke2020 project with local Bird UPM dependency and Oculus XR Plugin 1.11.2 using installed Unity 2020.3.33f1 Android support. Added reproducible Invoke-UnityQuestBuild.ps1, UnityQuestBuild.cs and UnityQuestSmoke.cs in lightweight tests. Builds IL2CPP ARM64/OpenGL ES3, min API 29/target API 30. Scene uses synthetic desktop Bird poses plus head-pose camera and a clear synthetic-input label. Preview shaders are explicitly referenced by scene materials to retain them in the APK. No real hand backend enabled.

Final build passed; adb install -r returned Success and pm path confirmed org.bird3d.questsmoke installed. Display name Bird Quest Smoke, activity com.unity3d.player.UnityPlayerActivity. APK at heavy Validation/QuestSmoke2020/Build/BirdQuestSmoke.apk; SHA256 119127CFF9196BDD77BEA1F6DA60CB78144691F31B7E3E23489F96E359FD5668. A first build/install also passed; the final build adds explicit shader retention. An intermediate runner attempt failed on an unnecessary nonexistent textrendering module dependency, which was removed before the successful final build.

Did not launch the installed app, honoring the user's test-later choice. Actual XR initialization, visual rendering, camera behavior/comfort, hand tracking and performance remain unverified. Next physical test: launch Bird Quest Smoke, confirm label/cyan-pink synthetic trails and stable head-tracked view, then inspect app-specific BIRD_QUEST_SMOKE logs. This is a local sideload smoke test, not modern-editor compatibility, a release-ready SDK choice or VRChat validation. No elevated installer prompts were retried. User can leave; USB authorization and installation need no further setup action at this checkpoint.


## Runtime interaction setup - 2026-09-23, 18:54 UTC cycle

Made BirdInteractable usable through ordinary runtime setup: initialized optional snap arrays/events, added Selected and Deselected listener properties backed by existing serialized fields, tolerated legacy null lists and ignored null/non-collider entries, guarded SnapToCollider's own-collider toggle for renderer-only targets. Existing Inspector event names and empty-cache/all-surfaces behavior are preserved. Documented listener lifecycle, cancellation semantics and Start-time cache behavior.

Validation: 137 actual Unity 2020.3.33f1 headless Play Mode checks passed, including six new runtime-setup checks and existing lifecycle/feedback/trail coverage. Existing interaction tests now use the public event API rather than reflection. No rendering rerun, headset polling, app launch or replacement of the installed Quest smoke APK. User chose visual testing later; that remains pending. Next integrate Bird-driven controls into the preview or continue optional adapter packaging; the installed headset smoke build remains available for the user's physical test.


## VRChat project foundation - 2026-09-23, 20:55 UTC cycle

Verified the user-installed Unity 2022.3.22f1 and Creator Companion; editor installation gate is complete. Bird's existing SDK-free local UPM package passes all 75 checks in a fresh Unity 2022.3.22f1 project. Installed .NET SDK 8.0.425 under LocalAppData/BirdTools/dotnet8 and official vrchat.vpm.cli 0.1.28 under BirdTools/vpm (no PATH or system SDK replacement). Set DOTNET_ROOT to that local runtime when invoking this vpm.exe.

Created heavy-repository BirdWorld using official VPM World template, then updated Worlds/Base to 3.10.5 and locked them in vpm-manifest.json. VPM emitted a failed-removal message for absent standalone com.vrchat.clientsim but reported successful resolution. Unity actually imported the Worlds/UdonSharp C# assemblies and ran the new lightweight tests/UnityVRChatWorldChecks.cs generator successfully, saving Assets/BirdWorld/Scenes/BirdFeasibility.unity with floor, spawn, descriptor, light and landmark. Scene overwrite is refused. SDK packages restore with VPM; generated caches/utility copies/builds/logs are ignored. The standard VPM resolver stays included with its license.

This completes editor/tooling, SDK-import and initial scene-scaffold substeps only. No Bird Udon program was compiled, no ClientSim behavior or VRChat client build was tested, and no world upload/authentication occurred. Existing standalone Quest smoke install was not polled, launched or changed. Next: add a small UdonSharp avatar hand-bone diagnostic, compile it with the real SDK, inspect missing-bone/scale behavior in ClientSim, then proceed toward a two-hand solver port and physical feasibility checks. Android SDK/NDK/JDK for the new editor remain a separate setup item.

Official setup references: https://vcc.docs.vrchat.com/vpm/cli/ and https://creators.vrchat.com/sdk/upgrade/current-unity-version/ (checked this pass).


## Udon hand-data probe - 2026-09-23, 22:56 UTC cycle

Added lightweight Integrations/VRChat/BirdHandDataProbe.cs with a stable meta and setup README. The separate UdonSharp diagnostic samples the local avatar wrist plus 15 finger bones per hand at 10 Hz, exposes availability counts/VR mode, positions 32 optional markers, hides zero/near-zero and nonfinite samples and clears on disable. No networking, sphere fitting, click detection, calibration or raw-joint claim. Bone availability and VR mode are not tracking confidence; zero at world origin is ambiguous and distal bones are not fingertips.

Real Worlds SDK 3.10.5/UdonSharp compilation succeeded in Unity 2022.3.22f1 after the compiler rejected enum-array indexing and TextMesh.text; final source uses integer bone IDs and a world-space UnityEngine.UI.Text label. The generator explicitly creates the program asset, uses CompileSync, checks compiler error state/retrievable program data and wires the backing behaviour. Saved heavy BirdWorld/Assets/BirdWorld/Scenes/BirdHandProbe.unity and Programs/BirdHandDataProbe.asset. Source/meta must be restored from the lightweight integration folder into ignored Assets/BirdGenerated/Runtime before opening a fresh checkout; see both READMEs.

Generation returned PASS, but Unity logged an ArgumentNullException from SceneTemplateAsset.CreatePipeline during its scene-saved callback. A separate process ran ValidateSaved successfully: all 32 hidden collider-free markers, UI font, script/backing-program references and retrievable compiled Udon data survived reopening. The scene-template editor exception is recorded rather than claimed resolved. This is compiler/serialization validation only; ClientSim runtime, physical hand tracking, scaling/avatar swaps and client build remain pending. No headset access, app launch or upload this pass. Next: execute the diagnostic under ClientSim and examine actual available/missing bone data before solver adaptation.


## ClientSim execution and lifecycle finding - 2026-09-24, 00:56 UTC cycle

Added tests/UnityClientSimProbeChecks.cs, an editor observer which enters Play Mode in the saved hand-probe scene with the actual SDK ClientSim. It does not call the C# probe Update; it reads live Udon program variables and referenced scene objects. ClientSim's default desktop avatar supplied left=16/16 and right=16/16 bones with IsUserInVR=False. The running label, per-hand counts and visible-marker counts agreed. This is actual ClientSim execution, not physical tracking evidence; desktop avatar bones do not establish finger tracking.

The full test currently FAILS automatic cleanup: disabling the backing component, and subsequently testing whole-GameObject deactivation, left marker activeSelf flags, counters and status text unchanged. Whole-object deactivation does hide the hierarchy, so this is stale state rather than evidence of visible geometry remaining. The compiled _onDisable entry point is present. A diagnostic direct backing.RunEvent("_onDisable") returned true and cleared all 32 marker flags, set counts to zero and changed the label to Disabled. That confirms the compiled handler works when invoked but does not satisfy the automatic event-delivery check. The root cause (editor observer timing, SDK lifecycle dispatch or production behavior) is not established; do not call it fixed or change SDK sources speculatively.

Fresh result files: clientsim-probe-baseline-result.txt PASS; clientsim-probe-result.txt FAIL with automatic/direct-dispatch evidence. These/logs are ignored under BirdWorld. No user global ClientSim settings, authored scenes, headset state or production probe source changed. Next investigate automatic lifecycle delivery using an in-frame runtime driver rather than EditorApplication.update, then test recovery and missing-bone/scale cases. Source harness intentionally preserves the failing assertion. The earlier scene-template exception is a separate issue.


## Frame-loop lifecycle investigation and explicit controls - 2026-09-24, 02:57 UTC cycle

Moved ClientSim assertions into UnityClientSimFrameDriver.Update, with both generated helpers compiled under UNITY_EDITOR in Assets/BirdGenerated/Runtime. The previous generated Editor copy must be removed when upgrading. The check still reads live Udon state and never calls the C# proxy Update. Automatic GameObject-disable cleanup failed again from Unity's frame loop, so EditorApplication.update timing alone does not explain it. Direct diagnostic Udon dispatch still clears markers/counts. No SDK modifications or claims of an automatic lifecycle fix.

Added production Udon custom events PauseProbe and ResumeProbe. Pause stops sampling, clears marker active flags/counts and labels the probe Paused; resume requests a fresh sample. OnDisable shares the clearing helper but its delivery remains unverified/failed in this harness. Use explicit pause before deactivating when the independent label must clear, and resume after reactivating a paused probe.

Validation on final source: RunExplicit PASS in actual ClientSim, left=16/16 right=16/16 VR=False, including a paused interval with zero counts/hidden marker flags and recovery after real Udon SendCustomEvent calls. Run (automatic lifecycle diagnostic) still FAILS, intentionally retained, with direct _onDisable dispatch=True and remainingActive=0 after diagnostic dispatch. Separate result files preserve this distinction. Updated the heavy project's compiled program-asset field metadata; authored scenes and pre-existing untracked Assets/XR files were preserved. No hardware access or upload. Missing-bone, scale/avatar-change, native client and physical tracking checks remain pending; next prioritize a controlled missing-bone/scale probe test or investigate SDK lifecycle event delivery without blocking independent solver work.


## Controlled missing-avatar recovery - 2026-09-24, 06:57 UTC cycle

Added UnityClientSimProbeChecks.RunMissingBones using the actual Unity 2022.3.22f1/Worlds SDK 3.10.5 ClientSim and live Udon heap. The runtime-only fixture temporarily clears the local avatar manager's private animator reference, exercising the SDK's missing-bone zero return without changing SDK sources, assets or global preferences. It requires nonempty baseline data on both hands, verifies zero API positions for all 32 requested bones, checks inactive marker flags and zero counts/status, restores the animator, and requires recovery to the original availability counts. Normal completion and failure restore the temporary reference. SDK provider/field changes fail explicitly.

Validation: RunMissingBones PASS, baseline and recovery left=16/16 right=16/16 VR=False. No production probe changes were needed. This is controlled missing-avatar data, not selective missing finger bones, a real avatar switch, physical tracking loss, scale validation or hardware evidence. Automatic-disable delivery remains unresolved and its separate failing diagnostic is retained. No headset access, launch, install or world upload this pass. Next test avatar scaling and begin a bounded Udon-compatible solver adaptation while keeping actual joint fidelity and physical clicking as separate feasibility gates.


## ClientSim avatar scaling - 2026-09-24, 08:58 UTC cycle

Added UnityClientSimProbeChecks.RunScale. Using the real VRCPlayerApi in Unity 2022.3.22f1/Worlds SDK 3.10.5 ClientSim, it changes runtime avatar eye height from baseline to half size, 1.5 times size, then baseline. At every step all 32 finite/nonzero SDK bone positions must match active live Udon markers within 2 mm, and counters/status must remain 16/16 per hand. Wrist-relative bone lengths must scale and recover within 2 mm; moving the player alone cannot satisfy that check. Completion/failure restores initial eye height without saving scene or global preference changes.

Validation: RunScale PASS. Eye heights 1.9 -> 0.95 -> 2.85 -> 1.9 m; left wrist-to-middle-distal spans 0.2158084 -> 0.1079042 -> 0.3237126 -> 0.2158084 m. No production probe changes were required. This validates simulator bone scaling and probe position following only, not solver scale normalization, actual avatar replacement, physical hand tracking or VRChat client behavior. Automatic-disable delivery remains unresolved; independent explicit pause/resume controls remain available. No headset access or world upload.

Next prioritize a small Udon-compatible sphere-fit solver and deterministic compiled-Udon fixture, preserving the existing spatial interaction and separating avatar-bone adaptation/calibration from the solver. Do not let the unresolved automatic-disable diagnostic block that implementation. Selective missing bones, actual avatar changes, physical clicks and multiplayer remain separate gates.


## Compiled Udon sphere fitter - 2026-09-24, 10:59 UTC cycle

Added local-only BirdSphereFit UdonSharp behaviour with 4-32 caller-selected points, Fit custom event, fitValid/center/radius outputs, bounded loops without per-fit collections, finite checks and cleared outputs on rejection. Centering and normalization precede a symmetric 3x3 covariance solve. It preserves the original algebraic least-squares objective while deliberately rejecting normalized determinants <= 1e-6 to avoid unstable near-planar fits. That guard is provisional until realistic hand-pose testing; the general Unity Bird implementation is unchanged. Zero remains a valid geometric point, so a future avatar adapter must handle missing-bone sentinels upstream.

Validation: actual Unity 2022.3.22f1/Worlds SDK 3.10.5 Udon compilation and 18 compiled-VM cases PASS under ClientSim. The test supplies heap inputs and sends the Fit custom event, never invokes the C# proxy Fit. Cases include analytic spheres, translation (including 100/-200/300 m center), 0.5x/1.5x hand-size spheres, tetrahedron, perturbed non-spherical data compared with the original centered 4x4 normal equations, null/short/oversized arrays, nonfinite/coincident/collinear/planar/near-planar rejection, cleared outputs and valid recovery. Valid center/radius agreement tolerance is 0.2 mm. The first 17-case run passed, then the final run added/passed noisy-reference agreement.

The fixture generates an ignored program asset and unsaved scene object. No authored world scene, existing hand probe, SDK source, headset or global preferences changed. This is synthetic compiled-Udon solver evidence, not avatar-bone adaptation, a complete cursor, click behavior, performance profiling or hardware tracking. Next add a bounded synthetic two-hand cursor demonstration using the solver, preserving Bird's range mapping and click hysteresis, then adapt/calibrate avatar bones explicitly rather than treating diagnostic bones as tracked fingertips.


## Compiled Udon cursor state - 2026-09-24, 13:05 UTC cycle

Added BirdCursorState, composing a dedicated BirdSphereFit through real Udon behaviour calls. Caller inputs are fit points, hand root, index tip and tracking; Step computes an unsmoothed cursor with Bird.cs's original range law and nearest-of-fitted-center-or-cursor selection sphere. Strict 7 mm press / 5 mm release thresholds preserve hysteresis. Outputs are poseValid, position, selected, down and up; pulses describe an input sample rather than an engine frame. Optional marker transforms follow valid samples and hide on rejection. Invalid/lost input and explicit Cancel release once and hold the last finite position behind poseValid=false. Each cursor owns its fitter.

Validation: Unity 2022.3.22f1/Worlds SDK 3.10.5 compiled-Udon ClientSim run PASS, 51 assertions. Tests cover cross-behaviour fitter calls, the original unfiltered range equation, extending distance, click press/hold/release/rearm, both selection centers, two-instance independence, tracking loss, degenerate fit, nonfinite tip, cancellation/repeated cancellation, recovery and marker transform/active flags. The harness sets heap inputs and dispatches Step/Cancel, never calls the C# proxy implementations. Two fixture cursors are temporary unsaved scene objects; generated program assets remain ignored.

This completes caller-fed cursor state only. No Kalman smoothing, twist, avatar root/tip mapping, scale-normalized thresholds, automatic input sampling, networking, rendered user-facing scene or physical tracking validation. Explicit Cancel must be sent before deactivation; automatic-disable delivery remains unresolved. Existing ordinary Unity Bird and the headset install are unchanged. Next build a clearly labeled synthetic two-cursor world scene with visible trails and click feedback, then address filtering and real avatar-bone adaptation without confusing distal bone origins with fingertip endpoints.


## Visible synthetic Udon scene - 2026-09-24, 15:06 UTC cycle

Added BirdSyntheticDemo input driver and heavy BirdSyntheticDemo.unity with two cursor/fitter instances, cyan/pink tapered trails, gold pressed feedback, per-hand click counters and explicit synthetic/no-hand-tracking labels. Each driver reuses four tetrahedral fit points and samples at up to ~33 Hz; trails retain at most 64 positions each (128 total). History is bounded by sample count, not guaranteed wall-clock fading under slow frames. PauseDemo cancels/clears and ResumeDemo starts fresh history. No avatar input, Kalman filter, networking or user-facing controls yet.

Saved three production Udon program assets and three Unlit/Color materials in the heavy repository. Source/metas and generator/validator remain lightweight. Generator refuses existing authored scene/program assets and removes only earlier ignored validation program assets for the same sources to avoid duplicates. Existing sphere/cursor fixtures now reuse authored programs when present. Restore all three source/meta pairs into ignored Assets/BirdGenerated/Runtime on a fresh checkout.

Validation: real Udon compilation, scene generation, separate saved-scene reopen and ClientSim runtime checks PASS. Both drivers produced clicks and bounded trails; explicit pause cleared trails/labels and resume recovered. A rendered Unity capture was inspected: title and synthetic labels, cyan/pink trails, gold left pressed cursor, pink right open cursor and both click counts are visible. The first capture was obstructed by simulator avatar/intro UI; the final capture temporarily isolates authored visuals on a camera layer and restores layers afterward without saving or changing ClientSim preferences. This is an authored-scene camera inspection, not interactive GUI or actual headset validation.

Unity again logged ArgumentNullException from SceneTemplateAsset.CreatePipeline during generation; successful save/reopen/runtime evidence does not resolve that editor callback issue. No headset access, install, VRChat upload or client/multiplayer test. Next add accessible demo controls and improve motion/visual feedback, then filtering and an explicit avatar-bone adaptation/calibration path. Keep synthetic and physical-input claims separate.


## Conventional synthetic-demo controls - 2026-09-24, 17:08 UTC cycle

Added local BirdDemoControl.Interact with shared pause/resume and clear-trail actions. The saved world now has labeled PAUSE/RESUME and CLEAR TRAILS box controls with enabled colliders and 3 m interaction proximity. Pause cancels both cursors and empties history; resume restarts them. New BirdSyntheticDemo.ClearTrail clears only history, preserving click counters and pause state. The pause control owns its toggle state; independent external pause calls must not be mixed with it. No Bird input is required for these conventional controls.

Validation: real Udon compilation, saved-scene reopen and compiled _interact dispatch PASS in ClientSim. Both demos paused/cleared and resumed; the clear handler emptied both trails, preserved the checked click counter and permitted rebuilding. Labels/colliders were checked. Inspected the rendered capture: both labeled buttons, synthetic input labels, trails and click feedback are visible. Direct VM event dispatch is not a physical ray/pointer/controller test or headset usability evidence. No hardware, upload or multiplayer test.

First import reported transient missing-type C# errors and then an outdated Udon script-version serialization error before saving the upgrade. Added an asset refresh and resumable matching program/material reuse; the retry saved successfully and final separate runtime/render validation passed. AddControls refuses duplicate scene controls or an existing program with a different source. Unity's known SceneTemplateAsset.CreatePipeline ArgumentNullException still appeared during save and remains unresolved. Fresh checkout now needs all four integration source/meta pairs. Next prioritize filtering and an explicit avatar-bone input/calibration path; the conventional controls are present but physical activation remains deferred.


## Optional Udon Kalman smoothing - 2026-09-24, 19:08 UTC cycle

Added optional smoothing and rawPosition output to BirdCursorState. The scalar-covariance Vector3 recurrence matches the existing Bird filter with Q=0.001 and R=270*d^3, using fitted-center distance from hand root. Click testing around the cursor uses its filtered position. Deliberate difference: first valid input seeds directly at the measurement; invalid/lost input and Cancel discard history, and the next valid input reseeds instead of drifting from a stale location. Disabling smoothing bypasses it, and reenable also reseeds. The general Unity Bird implementation remains unchanged. This is per-sample smoothing, not frame-rate normalization.

Validation: 97 compiled-Udon cursor assertions PASS in Unity 2022.3.22f1/SDK 3.10.5 ClientSim. Includes prior raw cursor/click checks plus 40 translated/jittered samples at two distance/noise levels compared with the actual original KalmanFilterVector3 C# implementation, attenuation, movement without overshoot, loss recovery, bypass and reenable. Original C# reference is copied only into ignored validation sources; production Udon has no dependency on it.

Enabled smoothing on both saved synthetic-scene cursors and updated the program metadata. Separate saved-scene ClientSim validation PASS with compiled interaction controls, pause/clear/resume and trail rebuilding; inspected a fresh rendered camera capture with both trails, labels and controls intact. Static rendering does not establish motion comfort. The known scene-template callback ArgumentNullException recurred during scene save and remains unresolved. No headset access/upload or physical tracking validation. Next prioritize an explicit avatar-bone input adapter and calibration/limitations rather than extending the synthetic demo indefinitely.


## Experimental avatar mapping and range finding - 2026-09-24, 21:09 UTC cycle

Added BirdAvatarInput, a local caller-fed adapter sampling avatar bone origins at up to ~33 Hz. Uses 12 fit origins: thumb intermediate/distal, index proximal, and all three bones of middle/ring/little; omits the four unavailable fingertip endpoints from ordinary Bird's 16-point set. Requires 14 finite/nonzero origins including thumb proximal for the weighted root and index distal as a finite placeholder. The adapter forces clicksAllowed=false; it never treats distal origins as tracked tips. Missing data cancels the cursor. Added a cursor-level click gate with default true, releasing existing selection once when disabled. No authored avatar-input scene or synthetic-scene replacement.

Actual Unity 2022.3.22f1/Worlds SDK 3.10.5 ClientSim mapping/root/placeholder, disabled-click, controlled missing-avatar cancellation and recovery checks PASS for both hands. The test observes actual Udon Update and compares positions against an independent list of expected bone names. Crucial finding: the default avatar supplies all required bones and yields accepted sphere fits, but the unchanged range law places cursors about 1,250 m away (right 1250.077 m, left 1249.509 m). Availability and solver acceptance therefore do not establish usable input.

Added an adapter-only maximumPreviewRange rejection guard, default 3 m. It cancels/hides excessive-range results, exposes measuredRange/rangeRejected and labels the need for calibration; it does not clamp or change the ordinary Bird/synthetic cursor range law. Final adapter test PASS with both default-avatar results correctly rejected, and loss/recovery handling intact. Cursor regression suite also PASS: 106 compiled-Udon assertions including click-gate release/suppression/reenable plus existing range, hysteresis, filtering and recovery checks.

Source/meta and fixtures remain lightweight; adapter program and two instances are generated/unsaved. Updated the heavy cursor program metadata for the new click gate and documented the finding. No SDK source/global preference changes, headset access, world upload or physical tracking validation. Next investigate avatar hand-size normalization and calibration using measured geometry; keep the default adapter out of user-facing scenes until its range is useful and the lack of real fingertip data is addressed honestly. Selective missing-bone tests, real avatar swaps and physical clicks remain pending.


## Avatar scale and range diagnosis - 2026-09-24, 23:10 UTC cycle

Added UnityAvatarInputChecks.RunScaleCalibration, measuring both default-avatar hands at 1x, 0.5x, 1.5x and restored size through real ClientSim APIs and compiled Udon. Records hand span, sphere radius, center-to-root distance, normalized radial fit residual, raw range and diagnostic baseline-hand-span-normalized range. Checks proportional geometry, original centered 4x4 fit agreement within 0.2 mm, original range-law agreement and normalized range stability. Runtime eye height is restored on completion/failure. Both initial measurement and final reference-comparison runs PASS.

Right hand: span 0.10152 m, fit radius 0.18290 m, center distance 0.18894 m at baseline. Raw ranges: 20.04 m at half size, 1250.08 m at baseline and 14220.97 m at 1.5x. Left hand is nearly symmetric. Center distance/span remains about 1.861; radial RMS residual/span about 0.05174. Baseline-hand-span normalization stabilizes the range near 1250 m, so removing uniform scale dependence alone does not create a useful baseline. The sixth-power term explains the strong raw size sensitivity, and reference-fit agreement argues against a numerical error in the Udon solve for this fixture.

Preserved the reviewed eight-row CSV and AVATAR-CALIBRATION.md analysis in the lightweight repository; the generated project CSV is ignored. No production range parameters, authored scene or headset state changed. Adapter preview rejection and disabled clicks remain in force. Next explore explicit neutral-pose preview calibration, separating relative size compensation from desired working range; require multiple poses and avatar-change/loss behavior before enabling an end-user mode. Missing fingertips and physical tracking fidelity remain independent unresolved gates.


## Explicit neutral preview calibration - 2026-09-25, 01:11 UTC cycle

Added opt-in CalibrateNeutral/ResetCalibration to the experimental avatar adapter. A fixed 24-step inversion anchors the chosen pose to neutralPreviewRange (default 0.3 m; positive finite target no larger than 3 m or the preview guard). Calibration stores fitted-center distance divided by the sum of middle-finger proximal/intermediate and intermediate/distal segment lengths. Live compensation uses segment lengths rather than the bend-dependent chord used for earlier diagnostics. A cursor rangeDistanceMultiplier (default 1) adjusts the input to the existing range law and its filter noise; pointing direction and nonlinear shape are preserved.

Calibration is runtime-only and never automatic. Missing required data, handedness change, explicit reset or a local OnAvatarChanged callback invalidates it; the next valid data does not silently restore it. Avatar-swap callback compilation is verified but actual swap event delivery remains untested. Clicks stay disabled and excessive preview ranges are still rejected. The adapter remains outside the authored synthetic scene.

Validation: actual compiled-Udon RunNeutralPreview PASS for both default-avatar hands at 1x/0.5x/1.5x scale. Raw target ranges remain within 2 mm of 0.3 m; controlled missing-avatar loss invalidates calibration, NaN target is rejected, and explicit recalibration/reset work. This is raw target agreement, not a no-lag claim for filtered movement. Original height restored after testing. Cursor regression PASS: 114 assertions, including multiplier mapping, zero/NaN rejection and recovery plus original click/filter/loss checks. No hardware, user-facing scene change or world upload.

Updated the cursor program metadata, calibration analysis and reproduction docs. Remaining calibration gates: multiple articulated poses, selective missing joints, avatar replacement, gesture fidelity, physical comfort and loss/recalibration ergonomics. Next test pose variation and range response using a controlled simulator bone fixture before exposing this preview in the world.


## Controlled finger articulation - 2026-09-25, 03:12 UTC cycle

Added UnityAvatarPoseFixture and RunPoseVariation to the actual ClientSim/compiled-Udon avatar harness. The runtime-only fixture saves six proximal finger rotations and Animator enable state, temporarily disables animation, rotates middle/ring/little proximal joints on each hand by +/-15 degrees about local Z, and restores everything on completion/failure. No production code, authored scene or SDK assets changed. Helper imports initially logged transient missing-type errors; Unity subsequently rebuilt successfully and executed the test.

PASS: actual SDK distal positions changed about 26.5 mm while summed middle-finger segment length stayed constant within 0.2 mm. Both hands retained calibration and valid fits, kept clicks disabled and responded in raw target range: neutral 0.3000 m, +15-degree fixture about 0.5792 m, -15-degree fixture about 0.1498 m, restored 0.3000 m within 2 mm. Marker/validity and preview guard consistency also passed. Saved all eight metric rows and analysis in lightweight docs. No headset access or upload.

This closes the limited controlled-articulation substep, not physical gesture fidelity or human open/close semantics. The adapter remains outside the authored synthetic demo; actual avatar swaps, selective bone loss, comfort and fingertip-based clicks remain unvalidated. Next create a separate explicitly labeled avatar-preview scene with conventional calibrate/reset controls, keeping clicks disabled and the synthetic demo intact; validate saved wiring and ClientSim behavior before any physical test.


## Saved experimental avatar-preview scene - 2026-09-25, 05:13 UTC cycle

Added a separate BirdAvatarPreview.unity with two local avatar-input/cursor/fitter chains, cyan/pink collider-free markers, per-hand diagnostic labels and conventional CALIBRATE/RESET controls with shared feedback. Added BirdAvatarControl and an opt-in requireCalibration input flag; scene inputs require calibration so markers remain hidden before calibration and after reset through subsequent samples. Clicks are serialized disabled and the adapter continues enforcing that policy. The synthetic demo is unchanged.

Promoted the adapter's ignored validation program into the tracked Programs directory preserving its GUID, and added the control program asset. Lightweight sources/metas and UnityAvatarSceneChecks remain authoritative; the generator refuses scene overwrite and mismatched program sources. Existing avatar fixtures now reuse the authored adapter program instead of duplicating it. Materials are reused from the synthetic scene.

Validation: actual Udon compilation and scene generation PASS; separate saved-scene reopen and compiled Interact handler checks PASS in ClientSim. Verified hidden startup, both calibrated raw targets at 0.3 m within 2 mm, reset staying hidden after sampling, successful recalibration, feedback text, enabled control colliders and collider-free cursor markers. Inspected the camera capture: both markers, experimental/no-tip-click labels, calibration feedback and both controls are visible. Camera isolates authored visuals from simulator avatar/UI; this is not physical pointer activation or headset usability evidence.

The known SceneTemplateAsset.CreatePipeline ArgumentNullException recurred during generation; successful save/reopen/runtime checks do not resolve it. No headset access, VRChat client build, upload or multiplayer validation. Next investigate an offline/native SDK world-build validation path and selective missing-bone behavior, preserving the experimental label and disabled clicks until physical input fidelity is established.

## Native SDK world bundle - 2026-09-25, 09:14 UTC cycle

Completed the build investigation started in the prior cycle. Added UnityWorldBundleChecks and explicit UnityWorldSdkSetup entry points, plus WORLD-BUILD.md reproduction notes. The project lacked the active-platform UDON/SDK defines, so the SDK's own define setup was applied and Unity restarted. The first chosen WorldBuilderV3 subclass was the wrong pipeline: its legacy V3SdkUI switch is inactive in this SDK. The normal registered VRCSdkControlPanelWorldBuilder is now used, retaining all SDK validation.

Validation then exposed outdated layer/collision settings. Applied the SDK's SetupEditorLayers and SetupCollisionLayerMatrix, verified their checks pass, and reviewed the project diff. Layer 3 is now Item, reserved3 moved to unused layer 22, and the SDK collision matrix is current. Build preprocessing also applied SDK graphics/quality/player settings and XR configuration; these reviewed project settings are saved. Pre-existing untracked Oculus loader/settings were automatically moved by the SDK from Assets/XR into its ignored package XR directory. Their asset hashes match the pre-build copies retained under ignored Validation/WorldBuild/PreexistingXR; no duplicate GUID copies were restored into Assets.

PASS: real SDK build-only Windows world bundle, 148695 bytes, SHA256 0E152FFDCBAB3909AA0579FD604D236AE56A360145594DCF796F4974E71DAF55, saved under ignored Validation/WorldBuild/BirdAvatarPreview.vrcw. The helper compiles Udon and builds a generated copy, leaving the authored preview scene untouched. A separate editor reopen/ClientSim regression PASS verified hidden startup, compiled calibrate/reset interactions, both raw targets at 0.3 m, hidden hold after reset, recalibration and disabled clicks. Inspected the fresh rendered capture: both markers, status text and controls remain visible. No error/exception lines appeared in the successful build log; this does not prove the previously observed scene-template callback issue is globally resolved.

No upload, VRChat client launch, multiplayer or headset test in this cycle. The earlier user-requested Quest smoke APK redeployment was separately confirmed by adb install Success and package-path verification; it still uses simulated hand poses and has not been visually tested. Next return to selective missing-bone and actual avatar-change behavior, then client-level world validation when appropriate. The original portable adoption flow, real fingertip clicks and hardware fidelity remain open.

## Selective avatar-data loss - 2026-09-25, 11:15 UTC cycle

Added UnityAvatarSelectiveLossFixture and RunSelectiveLoss to the actual Unity/ClientSim avatar harness. The runtime-only fixture wraps the SDK GetBonePosition delegate for one local-player bone at a time and forwards all other queries to ClientSim. All avatar-check entry points now require this companion plus UnityAvatarPoseFixture to compile. No production adapter, authored scene or SDK source changed.

PASS: 31 compiled-Udon scenarios in Unity 2022.3.22f1/Worlds SDK 3.10.5: zero for each of the 14 required bones on each hand, NaN and infinity cases, and an unused index-intermediate control. With requireCalibration true, required-bone loss produces exactly 13/14 availability, no dataReady/tracking/valid marker, cleared calibration and rejected calibration while missing. The unaffected hand remains calibrated and valid. After data returns, the affected hand remains hidden until explicit recalibration; both raw targets then agree with 0.3 m within 2 mm. Clicks remain disabled. The unused-joint control leaves both hands working. Runtime delegate restored on completion; no saved asset mutations.

The first helper import briefly logged missing-companion compiler errors before Unity refreshed/recompiled and ran the passing suite. This is actual Udon VM behavior under SDK return-value fault injection, not proof of genuine avatar bone removal, avatar-swap event delivery or physical tracking loss. Updated calibration/reproduction docs and the completed substep; the broader hardware/avatar-change/lifecycle gate stays unchecked. Heavy repository remains unchanged, so no no-op commit there. Next investigate actual ClientSim avatar-change delivery and automatic-disable cleanup, keeping physical usability deferred.

## Avatar-change event routing - 2026-09-25, 13:15 UTC cycle

Inspected SDK 3.10.5 ClientSim source: ClientSimPlayerHeightManager emits _onAvatarChanged on player join through its Udon event sender; no avatar-replacement implementation was found. Added RunAvatarEvents to the actual Unity avatar harness rather than labeling a manual callback as a real avatar swap. All inputs require calibration in this fixture; no production code, authored scenes, SDK assets or hardware state changed.

PASS in Unity 2022.3.22f1/ClientSim/compiled Udon: both hands calibrated; a real simulator remote-player spawn/join preserved local calibration; SDK-transport replay of a remote avatar event also preserved it. Replay of the local avatar event immediately cleared both adapters' dataReady/calibration, cursor validity and marker visibility. Later valid samples stayed hidden until explicit recalibration restored both raw targets to 0.3 m within 2 mm. Clicks stayed disabled. The fixture removed its remote player and checked the original player count, with cleanup also present on failure.

Documented reproduction and ticked only event-routing validation. Actual avatar replacement, networked multiplayer, automatic-disable cleanup and physical input remain unverified. Heavy repository is unchanged; only the meaningful lightweight test/docs checkpoint is committed. Next investigate the remaining automatic-disable lifecycle issue, then improve the experimental world while keeping avatar/fingertip/hardware claims limited to the evidence.

## Unity Hub CLI availability note - 2026-09-25

Dana updated Unity Hub and reports its banner advertises a new Unity CLI. Consider that CLI for future editor/module setup before relying on GUI installation. A quick Get-Command check found no unity, unity-cli or unityhub command in this session's PATH; this does not establish the executable's actual name or installation location. Discover the installed CLI and inspect its help before using it. No editor/module changes were made for this note.

## Probe automatic-disable cleanup - 2026-09-25, 15:16 UTC cycle

Reproduced the original automatic GameObject-disable failure under the now-configured SDK: marker flags/counts remained stale, while diagnostic direct _onDisable dispatch cleared them. Traced SDK 3.10.5 UdonManager.Update: it iterates registered behaviours before draining queued unregistrations; UdonBehaviour.ManagedUpdate dispatches _update without an enabled/active check. Thus a final queued sample can undo OnDisable cleanup. Added a small production guard in BirdHandDataProbe.Update for enabled and activeInHierarchy, preserving the paused guard.

Strengthened the observer to require immediate OnDisable marker/count/label clearing, then retained its later disabled-interval and re-enable recovery checks. Added RunComponentDisable using the backing component's enabled flag with normal SDK proxy synchronization. Both separate Unity 2022.3.22f1/compiled-Udon ClientSim runs PASS: GameObject deactivate/reactivate and component disable/re-enable, restoring 16/16 counts and marker/label agreement. No direct event dispatch is used to obtain either passing result. No authored scene, SDK source, headset or global preferences changed; heavy repository has no meaningful diff.

Updated current reproduction notes and checked off the probe-specific lifecycle result. This fixes the diagnostic probe only. Cursor state and avatar/synthetic drivers still require explicit cancellation before external deactivation; extend lifecycle handling and tests to those components next. Actual avatar replacement, physical tracking, headset comfort and multiplayer remain unverified.
