# Focused checks

Run `pwsh -NoProfile -File tests/Test-OpenXRHand.ps1` from the repo root in a fresh PowerShell process.

This compiles the actual Hand, HandFactory and OpenXRHand source with BIRD_OPENXR_ENABLED against deliberately small API doubles. It checks both hands, startup without a subsystem, valid/failed pose reads, shutdown, restart and tracking loss. Pose is a value type, so the original null comparison fails compilation. A failing TryGetPose deliberately returns nonzero data to detect incorrectly consuming its output.

These tests do not validate Unity package resolution, real XR Hands API compatibility, joint mappings, coordinate transformations, Udon, or device behavior. Full Unity compilation and headset checks remain required. No binaries or generated test projects are committed.

## Production solver checks inside Unity

Run the following in PowerShell, adjusting the editor path/version as needed:

```powershell
./tests/Invoke-UnityCoreChecks.ps1 -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2020.3.33f1/Editor/Unity.exe' -UnityVersion '2020.3.33f1' -ProjectPath '../bird-3d-cursor-projects/Validation/Core2020'
```

The runner generates a disposable project in the demo repository, copies the current production Hand/HandFactory/Bird/Kalman sources and the editor checks, and launches Unity in batch mode. It refuses existing directories without its marker and requires both a successful exit and a fresh PASS result. Logs and results remain inside that generated project; do not commit its source copies or Library.

These checks use actual Unity math and production solver code with synthetic input: known sphere center/radius, index-finger clicking, lost tracking during press/release, continued loss and recovery. They establish core editor compilation/execution, not full package import, XR Hands API compatibility, rendering, Udon or device fidelity. A tracking-loss release follows the existing click-up API; consumers requiring explicit cancellation should not interpret it as a confirmed user action.

Batch Kalman-filter checks compare list input against chronological single-measurement updates, including one/multiple samples, oldest/newest-first ordering, noise overrides and the next measurement's result. Empty batches preserve the existing zero return value and leave filter state unchanged.

Invalid-pose checks inject NaN into a fitting joint, positive infinity into the index tip, negative infinity into the thumb base, and an all-zero hand. Each must hold the last valid cursor/geometry, release selection once, clear stale motion and recover without contaminating filter state. These do not yet establish conditioning thresholds for coplanar or nearly singular point sets, or track individual joint validity when an adapter substitutes finite zero coordinates.

## All-source compilation

Add `-AllSources` and use a separate generated project path (for example `../bird-3d-cursor-projects/Validation/AllSources2020`) to compile every production script, including BirdProvider, BirdManager, BirdInteractable and the editor configuration window, then execute the core checks. The runner includes Unity's physics and IMGUI modules. A mode marker prevents accidentally mixing core-only and all-source runs in one project.

Tracking defines are absent in a fresh generated project, so optional Leap/OVR/XR Hands adapter bodies remain excluded. This mode copies scripts into Assets: it is **not** a UPM installation test, player build, enabled tracking-adapter compile or live behavior test for provider/interactable components. Keep the generated project dedicated to this runner rather than changing defines or copying unrelated scripts into it.

## UPM installation

Use `-Package` instead of `-AllSources`, with a new project directory such as `../bird-3d-cursor-projects/Validation/Package2020`. The generated manifest references Unity/BirdPlugin via a local file dependency, and no production scripts are copied into Assets. Core tests run against Bird3D.Runtime and assert that Bird3D.Editor is separately loaded. The base package supplies physics and IMGUI dependencies; no hand SDK or tracking symbols are required. This validates UPM import and editor compilation, not player builds or enabled hardware adapters. See Unity/BirdPlugin/README.md for the current migration limits.

## Standalone player build and startup

Use `-Package -BuildPlayer` with a separate directory, for example `../bird-3d-cursor-projects/Validation/PackagePlayer2020`, and the same editor/version parameters. This builds a Windows x64 Mono player with a generated smoke-test scene, checks that Bird3D.Runtime.dll is included and Bird3D.Editor.dll is excluded, then runs the player headlessly. The player instantiates Bird with untracked synthetic input and checks idle state and runtime assembly loading without UnityEditor assemblies.

Both build and player must exit successfully and write fresh PASS results. Build/import has a three-minute timeout and the player a thirty-second timeout. Artifacts and logs remain in the generated project; this is local validation output, not a release build. This mode does not rerun the editor checks, exercise rendering or provider/interactable scene behavior, or validate IL2CPP, Android, XR hardware or VRChat.

## Backend registration checks

The editor suite also covers missing constructors, availability, both chiralities, replacement, null constructor/output rejection and removal. The OpenXR API-double suite explicitly registers its adapter and creates both hands through the factory; doubles do not execute Unity initialization callbacks. The standalone player instead uses a synthetic backend registered by a real `BeforeSceneLoad` callback, then creates its Bird through the factory in Start. This validates initialization in a fresh player; repeated Play Mode entry with domain reload disabled and actual SDK adapter loading remain untested.

## Interactive desktop preview

Use `-Package -Preview` with a separate generated directory, such as `../bird-3d-cursor-projects/Validation/Preview2020`. This imports the UPM package, copies only its optional DesktopPreview sample into Assets, and compiles/generates `Assets/BirdDesktopPreview.unity`. The runner's PASS means compilation and scene generation only. Open that project in Unity 2020.3.33f1, open the scene, and enter Play Mode to inspect the visual controls. This mode neither builds a player nor reruns the editor assertions. All generated scene data remains in the demo repository's ignored Validation folder.

### Preview Play Mode integration checks

Add `-PlayMode` to `-Package -Preview` to generate a separate check scene and enter Unity Play Mode. A generated-project-only runtime checker drives the same private controls used by the sample GUI and observes actual solver state, cursor/ray transforms, marker visibility and resource cleanup across frames. It catches runtime errors and writes a fresh result before exiting Unity. The interactive preview scene is saved before adding the checker, so opening `BirdDesktopPreview.unity` does not automatically run tests or quit the editor.

Checks cover two distinct cursors, known fitted radii, increased range on opening, selection sizing, single press/release edges, loss/recovery visibility, held position and removal of generated camera/geometry/materials while preserving unrelated children. The run uses `-nographics`: it does not test shader output, GUI layout/input events, or hardware. It does not rerun the 75 core/package checks. The runner retains its three-minute outer timeout for startup/import/play failures that never reach the checker.

### Camera render validation

Add `-RenderPreview` to `-Package -Preview -PlayMode` to launch Unity in batch mode with graphics enabled. The generated project includes Unity's image-conversion module solely for PNG output; the distributed Bird package does not acquire this dependency. The checker renders the sample camera at 1280 by 720 in idle, selected and lost-pose states, saves `preview-idle.png`, `preview-selected.png` and `preview-lost-pose.png`, and requires a non-null graphics device plus visible expected-color pixels near both projected cursor positions in every capture. Old captures are removed before each render run; PASS still requires all runtime checks and a successful exit.

These are camera RenderTexture captures, not screenshots of the Unity editor or Game view. They exclude IMGUI controls and do not validate GUI input/layout, animated poses across their entire range, other aspect ratios, render pipelines, or standalone shader stripping. Inspect the PNGs separately for framing and appearance. The 94-check run passes on Unity 2020.3.33f1 with Direct3D 11 locally.

Trail assertions additionally cover fixed point capacity and ring ordering, distance/time sampling, stationary fading and expiry, explicit clear, invalid coordinates, clock rewind, loss/recovery without a connecting segment, preview integration and trail material cleanup. Render mode drives a short synthetic sweep and saves `preview-trails.png` for visual inspection, with both cursor colors checked as in the other captures. The added geometry was inspected with Direct3D 11; actual GUI button clicks, device profiling and multiplayer remain untested.

### Interactable event lifecycle

The preview Play Mode suite also drives a real BirdInteractable against the synthetic Bird through a disabled BirdProvider with its solver injected. This deliberately bypasses SDK/prefab provider startup. Checks cover idle frames, one press/hold/release, tracking loss, component disable/re-enable, provider loss, and event-only Drag/Toggle/Touch behavior. The test explicitly synchronizes collider transforms before touch entry/exit. This adds 12 assertions, bringing the non-render Play Mode suite to 85; the earlier 94 rendering-inclusive run predates these assertions. Rendering was not rerun for this event-only change.

Manager lifecycle coverage adds nine assertions, bringing the latest headless Play Mode run to 94. Tests use a disabled synthetic provider and named detector fixture objects: null-subscriber destruction, duplicate registration, callback-visible membership, missing-layer cleanup, enter-only/exit-only listener retention, unused cleanup and repeated unregister. They do not exercise actual trigger physics or SDK-backed provider startup. This 94-check headless result is distinct from the earlier 94-check rendering run, which covered fewer lifecycle cases but included camera assertions.

Full provider startup coverage now adds 18 assertions (nine per hand), bringing the latest headless suite to 112. Unlike the earlier injected-provider tests, this registers a synthetic Hand factory, creates real marker clones and allows Unity to run BirdProvider.Start and Update normally. It checks default/custom identity during registration, chirality, solver/marker updates, debug initialization, and destruction of every generated clone while preserving caller-owned objects even after public reference reassignment. This verifies provider lifecycle with synthetic input, not hardware adapter initialization or rendering.

Feedback coverage drives BirdInteractableVisual from the real interactable: hover state/color/scale, selection priority/color/scale, disabled-source idle behavior, unchanged shared material, and restoration of the original property block/scale. The latest rendering-inclusive run passes 146 assertions (112 previous headless, 21 existing render/sweep, eight feedback behavior, five feedback capture checks). `preview-feedback.png` is a camera capture of the selected orange visual child; its test placement deliberately avoids occluding the two cursor probes. This is not a complete UI or actual GUI input test.


### Optional XR Hands assembly

The XR Hands adapter source is now Runtime/XRHands/OpenXRHand.cs. Its asmdef is constrained by BIRD_OPENXR_ENABLED and references Unity.XR.Hands outside the SDK-free core. Test-OpenXRHand.ps1 still uses API doubles; it does not validate assembly linking against the real SDK. Package import and Windows player checks exercise the symbol-disabled, SDK-absent configuration. AllSources includes the moved source with its guard disabled. Real SDK-enabled compilation and device tracking remain pending.


The latest preview rendering run passes 163 checks, including radial rotation about a translated origin, per-copy point budgets, unchanged/changed multiplicity, inactive copies, tracking loss, invalid input, clear, duplicate-renderer rejection and live six-copy preview integration. RenderPreview also produces preview-mandala.png. Camera rendering does not exercise IMGUI controls. Real hardware performance remains unmeasured.


### Quest deployment smoke build

Invoke-UnityQuestBuild.ps1 -UnityEditor <Unity.exe> -ProjectPath <new generated project directory> builds Build/BirdQuestSmoke.apk. This dedicated harness uses Unity 2020.3.33f1 with its Android SDK/NDK/OpenJDK support, Oculus XR Plugin 1.11.2, IL2CPP ARM64, OpenGL ES3 and Android API 29 minimum/30 target. These are a local sideload smoke configuration, not a modern store release or VRChat configuration. It installs no modern editor and changes no existing demo project. Generated project/assets/builds belong under the heavy repository's ignored Validation folder.

The app identifier is org.bird3d.questsmoke and the display name is Bird Quest Smoke. Install explicitly with adb install -r <APK>, then verify with adb shell pm path org.bird3d.questsmoke. Installation alone does not prove successful XR initialization or rendering. When ready to test, launch its UnityPlayerActivity or select it on the headset. It displays two synthetic Bird cursors/trails, a diagnostic label, and drives its camera from head pose. BIRD_QUEST_SMOKE log lines report XR device activation. No hand-tracking backend is enabled, no network permissions are deliberately requested, and no account or store publishing is involved. In-headset rendering, comfort and actual hand tracking are separate validation gates.


Runtime interaction setup checkpoint: 137 headless Play Mode checks pass on Unity 2020.3.33f1. The fixture now wires Selected/Deselected through the public API and exercises legacy null snap lists, fresh no-listener targets, ignored invalid snap entries and collider-free snap mode. This run does not rerun the earlier 163-check camera-render suite or update the installed Quest APK.


Unity 2022.3.22f1 validation: the unchanged -Package runner passes all 75 core/factory/assembly checks in a fresh Package2022 project. This is SDK-free package import and execution in the VRChat-supported editor; it does not establish Udon compatibility or SDK-enabled hand tracking. UnityVRChatWorldChecks.cs is a separate one-time scaffold generator for a dedicated Worlds SDK project; it refuses to replace an existing feasibility scene. It checks actual Worlds/UdonSharp C# assembly references and scene construction, not Udon bytecode or a client build.


VRChat hand-data diagnostic: UnityVRChatProbeChecks.Run explicitly creates its UdonSharp program asset and uses the SDK synchronous compiler before serializing a backing behaviour. The compiler exposed unsupported HumanBodyBones array indexing and TextMesh.text; the probe uses integer bone IDs and UnityEngine.UI.Text instead. ValidateSaved is a separate read-only scene-reopen check. These are real SDK compiler/serialization checks, not ClientSim runtime tests. See Integrations/VRChat/README.md for source/meta restoration.


### ClientSim probe runtime check (known failing lifecycle assertion)

Copy UnityClientSimProbeChecks.cs and UnityClientSimFrameDriver.cs to BirdWorld/Assets/BirdGenerated/Runtime (remove the old generated Editor copy if upgrading) and run Unity 2022.3.22f1 with -batchmode -nographics -executeMethod UnityClientSimProbeChecks.Run. Restore the integration source/meta first as described in Integrations/VRChat/README.md. ClientSim/spawnPlayer must already be enabled; the harness does not change global preferences. It enters Play Mode without saving scene edits and reads the live Udon heap, not the C# proxy Update.

Observed baseline PASS: the SDK default desktop avatar supplied 16/16 bones per hand, and live counts/label/marker visibility agreed. Full lifecycle result currently FAILS: deactivation left stale marker flags and counters. Direct diagnostic Udon dispatch of _onDisable cleared them, so callback delivery/test timing remains under investigation. Direct dispatch is not treated as a pass for automatic cleanup. Re-enable recovery, missing bones, scale changes, visuals and physical tracking remain unverified. Separate fresh baseline/full result files prevent conflating these levels.


The observer now runs from a generated MonoBehaviour Update in Play Mode, not EditorApplication.update. Both helper sources are wrapped in UNITY_EDITOR and never enter a client build. RunExplicit verifies PauseProbe/ResumeProbe via real Udon SendCustomEvent calls, with a separate clientsim-probe-explicit-result.txt. That mode passes clearing, a paused interval and resumed label/count/marker agreement (16/16 per hand, desktop). Run retains the automatic GameObject-disable diagnostic; direct _onDisable dispatch after failure is diagnostic only. No global ClientSim preferences or authored scene state are changed.


RunMissingBones uses the same ClientSim setup and writes clientsim-probe-missing-result.txt. It requires a nonempty baseline on both hands, temporarily clears the local ClientSimPlayerAvatarManager private avatarAnimator reference, checks SDK zero returns for all 32 requested bones and live Udon marker/count/label clearing, then restores the animator and checks baseline availability recovers. The fixture is SDK 3.10.5-specific, fails explicitly if its field/provider changes, restores its runtime reference on normal completion or failure, and saves no scene or SDK assets. This is a controlled missing-avatar test, not an actual avatar switch or physical tracking-loss test.


RunScale uses the same ClientSim setup and writes clientsim-probe-scale-result.txt. Through VRCPlayerApi.SetAvatarEyeHeightByMeters it changes the runtime avatar eye height to 0.5x, 1.5x and the original value. At baseline and each step it requires all 32 finite/nonzero SDK positions, active Udon markers within 2 mm of those positions, and 16/16 counts plus matching label. Wrist-relative bone lengths must scale and recover within 2 mm, so a changed player position alone cannot pass. The initial avatar must have measurable hands and an eye height that permits these unclamped scales. Completion/failure restores the original eye height; no global preferences or authored scene are saved. This checks simulator data and probe following, not solver calibration or physical hand tracking.


Compiled sphere fitting: UnityUdonSphereFitChecks.Run executes BirdSphereFit through real Udon in ClientSim. Restore its source/meta and the editor-only test helper into BirdWorld/Assets/BirdGenerated/Runtime; use Unity 2022.3.22f1 batchmode/nographics with that executeMethod. It creates only an ignored generated program asset and an unsaved scene object. Checks cover analytic spheres, translated/scaled sets, a tetrahedron, noisy input against the original centered 4x4 least-squares equations, null/count/nonfinite/degenerate rejection, cleared outputs and recovery. Analytic/reference agreement tolerance is 0.2 mm. See Integrations/VRChat/README.md for the provisional degeneracy guard and remaining hand-adaptation limits.


UnityUdonCursorChecks.Run exercises the composed BirdCursorState/BirdSphereFit through compiled Udon in ClientSim. Restore both integration sources/metas and this editor-only helper into BirdWorld/Assets/BirdGenerated/Runtime, then execute with Unity 2022.3.22f1 batchmode/nographics. It tests the original unfiltered range law, press/hold/release hysteresis, both selection centers, sample pulses, two-instance independence, tracking loss, degenerate/nonfinite input, explicit cancellation, recovery and marker transform/active state. Authored scenes are not saved. The result is udon-cursor-result.txt; no filtered-cursor, actual hand, rendered demo or multiplayer claim follows.


UnityUdonDemoChecks.Generate creates the saved synthetic world scene, production program assets and simple materials in BirdWorld; it refuses existing scene/program assets. It removes only prior ignored validation program assets for the same three sources to avoid duplicate Udon programs. Restore all three integration sources/metas before generation. Validate reopens the scene and checks live ClientSim click counters, 2-64 trail points per cursor, labels and explicit pause/clear/resume. Run Validate with rendering enabled (omit -nographics); inspect Validation/UdonDemo/synthetic.png separately. Earlier cursor/sphere fixtures reuse the authored program assets when present. Rendering evidence is separate from physical tracking, VRChat client and multiplayer behavior.


Demo controls upgrade: restore BirdDemoControl source/meta alongside the other three integration sources, then run UnityUdonDemoChecks.AddControls once after Generate (or on the earlier scene). Validate now requires controls and dispatches their compiled _interact handlers to pause/resume both demos and clear/rebuild trails without resetting clicks. It also checks collider availability and toggle labels. Physical ray/pointer/controller activation remains untested.


Cursor smoothing checks additionally require a generated copy of Unity/BirdPlugin/Runtime/Scripts/KalmanFilterVector3.cs, the original ordinary C# reference. The compiled Udon filter is compared across 40 translated synthetic samples and two distance/noise levels against that reference after explicit seeding. Checks also cover jitter attenuation, movement without overshoot, tracking-loss reseeding, bypass and reenable. Existing raw range/click checks keep smoothing disabled. The production Udon implementation does not invoke or depend on the C# reference class.


UnityAvatarInputChecks.Run checks experimental BirdAvatarInput in actual ClientSim. Restore its source/meta and helper into the generated runtime folder alongside cursor/fitter sources and authored programs. The fixture independently checks the 12 expected bone names per hand, weighted root, index-distal placeholder, disabled clicks, fit/marker agreement and controlled whole-avatar loss/recovery. It uses the same SDK-private animator fixture as missing-bone checks and restores it on completion/failure. No authored scene is changed. Cursor checks also exercise disabling clicks while selected, a single release pulse, suppressed repeat clicks and reenable.
