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
