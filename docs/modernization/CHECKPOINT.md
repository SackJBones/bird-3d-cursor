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

## Actual scheduled smoke test — 2026-09-22

The app triggered this conversation at 06:45:33 UTC. Before testing, automation_update restored the normal prompt and two-hour cadence. The saved configuration confirms ACTIVE, heartbeat, and the same target conversation.

This scheduled run received `danger-full-access` filesystem/network access and approval policy `never`, matching the current conversation environment. This is broader than the restricted environment used during the initial manual preflight; no permission settings were changed by the agent. The automation API has no separate permission-profile field.

- Repository read/write: passed; initial working tree was clean on feature/vrchat-modernization.
- Test execution: passed, 36 OpenXR contract checks using API doubles. No Unity, Udon, or headset validation is implied.
- Commit/push: passed without approval prompts. Commit `68d4bcd` was pushed successfully to origin/feature/vrchat-modernization by this scheduled run.
- Scheduler smoke test: passed. The normal two-hour cadence is restored and active.

Computer must remain on with the app running for local scheduled work. The initial smoke test is a one-time step; subsequent scheduled runs should follow PLAN.md rather than repeat a checkpoint-only test.

## User steering — 2026-09-22

Headset setup is explicitly deferred until the user says it is ready. Do not poll the device or repeat setup requests. Continue all independent implementation and desktop/editor validation. Keep real tracking, usability and device performance testing deferred and clearly labeled; the headset must not block progress on the rest of the project. The recurring prompt and plan reflect this priority.

## Scheduled development pass — 2026-09-22, 08:45 UTC

Fixed stale click edges and held selection when tracking disappears. Bird now resets per-update press/release edges before checking tracking, emits one release if selection was held, and clears stale movement by setting the previous position to the held cursor position. Normal tracked sphere fitting and click thresholds are unchanged. This uses the existing click-up API, so consumers that distinguish cancellation from user release will need an explicit future cancellation API.

Added tests/Invoke-UnityCoreChecks.ps1 and UnityCoreChecks.cs. They copy the current production Hand/Bird/Kalman sources into a generated project under the heavyweight repo's ignored Validation directory and run inside the installed Unity editor. No maintained source copies, Library contents, or bulky assets are committed. Initial harness creation pulled default Unity packages; adding ProjectSettings/ProjectVersion.txt before launching prevents this new-project initialization on future runs.

Validation evidence:

- Before the fix, actual Unity 2020.3.33f1 compilation/execution reached the expected failure: `Tracking loss must clear the prior press edge` (exit 1).
- After the fix, the same runner passed all 15 core checks (exit 0): known sphere center/radius, index penetration, startup without tracking, loss on press/release, continued loss, and recovery.
- Existing 36 OpenXR API-double checks still pass. Git whitespace check passed.
- This is real Unity core execution with synthetic input, not full package import, XR Hands API compilation, Udon validation, rendering or physical-device validation. The headset was not accessed.

Next bounded work: degenerate/nonfinite sphere input and recovery motion tests, then actual XR Hands/package compilation on the supported modern editor. Separately observed during inspection: KalmanFilterVector3.Update(List<Vector3>) advances its index before reading and needs a regression-backed repair. Leave that independent fix for a later checkpoint.

## Scheduled development pass — 2026-09-22, 10:46 UTC

Repaired the batch Kalman-filter overload identified above. It previously advanced the index before reading, skipping the first chronological measurement and eventually throwing out of range for every nonempty list in either ordering. It now consumes each sample before advancing. The scalar filter equations and empty-batch behavior are unchanged. This repairs the public batch API; the existing Bird cursor uses the scalar overload.

Actual Unity 2020.3.33f1 regression run before the fix failed with ArgumentOutOfRangeException from KalmanFilterVector3.Update(List<Vector3>). After the fix, all 33 core checks passed (15 existing plus 18 batch checks): single/multiple samples, both list orderings, default/overridden noise, subsequent filter state and empty input. Git whitespace check passed. No full package, modern editor, XR Hands, VRChat or physical-device validation was performed in this checkpoint. No headset access.

Next: resume degenerate/nonfinite sphere input and recovery-motion validation, followed by modern editor/package setup. The batch-filter issue is resolved.

## Scheduled development pass — 2026-09-22, 12:47 UTC

Added validation before committing a hand pose to Bird's public geometry or smoothing filter. Reject nonfinite root/index-tip/fitting-joint inputs, nonfinite sphere results, nonpositive radius or pointing distance, and nonfinite range-mapped measurements/noise. Rejection holds the previous valid cursor/geometry and uses the same one-time release behavior as tracking loss. The valid-pose mapping and filter equations are unchanged.

Actual Unity 2020.3.33f1 regression evidence: before the fix, a NaN fitting coordinate failed `Invalid pose must hold cursor position; mode=0`. After the fix, all 61 checks passed: the previous 33 plus 28 assertions across NaN fitting input, positive-infinite index tip, negative-infinite thumb base, and all-zero hand input. Recovery is compared against an independently advanced valid-input Bird instance to verify that rejected frames do not poison its filter. Whitespace check passed. No headset access; full package/XR Hands/Udon validation remains pending.

Limits: this is not a complete conditioning policy for coplanar/nearly singular point clouds; finite zero sentinels for individual missing joints still need an explicit adapter validity contract. Changed-pose reacquisition motion is also untested. Next pass should prioritize modern Unity setup and package integration rather than indefinitely extending isolated core tests.

## Scheduled development pass — 2026-09-22, 14:47 UTC

Extended Invoke-UnityCoreChecks.ps1 with `-AllSources`, using a separate generated project and validation-mode marker. It copies all 11 production source files into Assets and compiles the provider, manager, interactable and configuration window as well as the core. Optional tracking adapter bodies remain excluded without their defines. The first run exposed a missing IMGUI module in the minimal harness; adding explicit physics and IMGUI modules resolved it. Unity 2020.3.33f1 all-source compilation and all 61 core checks passed. This is not a UPM install or a player build.

Rechecked VRChat's supported editor: 2022.3.22f1. Unity's official release page supplies changeset 887be4894c44. Attempted installation through the existing Unity Hub headless CLI. Hub's release/config requests returned 404, then its elevation request was declined/dismissed and Hub logged cancellation. No 2022 editor was installed. Do not automatically retry elevation or work around the dismissal; leave editor setup deferred while independent package work continues. Logs are in the local temporary directory as bird-unity2022-install.txt and bird-unity2022-install-error.txt, with the cancellation in the local UnityHub info-log.json. No headset access.

Package audit: package.json unconditionally depends on Ultraleap 6.8.1 despite backend compile guards; there are no tracked asmdefs, and .gitignore excludes asmdefs/meta files. A real UPM import path needs optional backend assembly boundaries and stable metadata, not just copying scripts. Next pass should implement/test the core package boundary on the available editor, keeping modern XR Hands and VRChat validation explicitly pending.

Sources: https://creators.vrchat.com/sdk/upgrade/current-unity-version/ and https://unity.com/releases/editor/whats-new/2022.3.22f1 .

## Scheduled development pass — 2026-09-22, 16:47 UTC

Established a baseline UPM package: Bird3D.Runtime and Editor-only Bird3D.Editor assembly definitions; moved the configuration window into Editor; tracked stable metadata instead of ignoring all .meta/.asmdef assets; replaced the unconditional Ultraleap dependency with the built-in physics and IMGUI modules. Added package installation/migration documentation and the package-local license. Existing guarded tracking sources are retained, but SDK assembly reference setup is not yet automated or validated. No serialized GUID migration from the legacy unitypackage is claimed.

Added `-Package` to the validation runner. A fresh generated Unity 2020.3.33f1 project resolved the actual local UPM dependency and executed 63 checks successfully (61 core checks plus runtime/editor assembly checks), with zero copied production scripts in Assets. No Ultraleap SDK or adapter symbol was needed. The existing all-source mode now accounts for the relocated editor script and removes only its obsolete generated copy in marked validation projects.

Next: player-build validation of editor/runtime isolation, then optional adapter assembly boundaries and explicit backend registration. Backend-enabled compilation, modern Unity and VRChat validation remain pending. Do not repeat the declined editor elevation request or access the deferred headset.

## Scheduled development pass — 2026-09-22, 18:47 UTC

Added `-Package -BuildPlayer` to the generated-project runner, plus editor build and runtime smoke-test scripts kept outside the distributed package. This separate mode builds a Windows x64 Mono player, requires Bird3D.Runtime.dll in its output and rejects Bird3D.Editor.dll, then launches the player headlessly and checks a fresh result and exit code. The runtime smoke test instantiates an untracked Bird, checks idle state/initial position and verifies no editor assemblies are loaded.

Actual Unity 2020.3.33f1 result: player build PASS, standalone startup/runtime smoke test PASS, both exit 0. Production code came from the UPM package, with no copied production scripts. The build used StrictMode and all generated artifacts remain under the heavyweight repo's ignored Validation/PackagePlayer2020 directory. Git whitespace check passed. The existing 63 editor checks were not rerun because this checkpoint changes only the validation tooling and adds a distinct player-build gate.

Limits: Windows Mono startup only; no rendered interaction scene, provider/interactable lifecycle, IL2CPP, Android, backend-enabled hand tracking or VRChat verification. This older-editor build is local validation output, not a distributable release. No headset access or editor-install retry. Next: optional backend assembly separation/registration and SDK integration.

## Scheduled development pass � 2026-09-22, 22:47 UTC

Removed concrete SDK constructor references from HandFactory. Backends register constructor delegates; callers can query availability and unregister, and missing registrations/null returned hands give explicit errors. Existing Leap/OVR/XR Hands adapters register before scene startup and expose a public registration method for edit-mode callers. Registration is cleared at Unity's SubsystemRegistration phase to avoid retaining callbacks between play sessions. Existing conditional enum names/values are unchanged. This is preparation for optional assemblies, not their completed separation.

Validation in actual Unity 2020.3.33f1: local UPM import and all 75 editor checks passed (63 prior plus 12 factory assertions). A Windows x64 Mono player rebuilt successfully and passed startup through a synthetic backend registered by Unity's real BeforeSceneLoad callback. Runtime/editor isolation still passed. All 36 OpenXR API-double checks passed, now constructing both hands via explicit adapter registration and the factory. Whitespace check passed. No headset access or editor-install retry.

Limits: no real SDK-enabled Unity compile, GUI/visual test, or VRChat validation. Domain-reload-disabled repeated Play Mode entry is handled by the reset hook but not yet exercised. Edit-mode factory callers now explicitly register an adapter first; this behavior and initialization order are documented in the package README. The conditional enum's serialized-value sensitivity remains unresolved and must be considered before backend selection changes. Next checkpoint: optional SDK assembly separation or a synthetic visual scene; keep the scope bounded.

## Scheduled development pass � 2026-09-23, 00:47 UTC

Added an optional Desktop Preview UPM sample. It creates two colored cursors, synthetic sphere-fit points, index-tip markers and root-to-cursor lines, driven by two production Bird solvers. IMGUI controls vary radius, animate orientation, simulate index penetration and toggle pose availability. Press/release counters expose both hands' click edges. Input is explicitly labeled non-anatomical synthetic data, with no hardware access. The component and generator source live in the lightweight repo; the generated scene is in the heavyweight repo's ignored Validation/Preview2020 project.

Added `-Package -Preview` to the runner. Actual Unity 2020.3.33f1 imported the package, compiled the sample, saved Assets/BirdDesktopPreview.unity and exited successfully. This gate verifies compilation/scene generation only, not Play Mode behavior or the existing 75 editor assertions. The preview targets the built-in render pipeline; other pipelines and standalone shader inclusion are untested.

Attempted GUI inspection using the computer-use skill. Unity loaded the generated project, but no targetable Unity window appeared in the desktop helper's window list; the helper's explicit launch attempt returned `launched app did not expose a targetable window`. No GUI clicks or Play Mode inspection occurred. Closed only the background Unity process created for this attempt. Do not call this preview visually or interactively validated. No headset access, SDK install or elevation retry.

Next bounded checkpoint: get the generated preview into Play Mode and verify visuals, radius changes, selection and one-time release on lost pose. Optional backend assembly separation and SDK-enabled compilation remain pending. Existing core tests were not repeated because production solver/factory code did not change.

## Scheduled development pass � 2026-09-23, 02:48 UTC

Added automated Play Mode integration checks for the Desktop Preview, invoked with `-Package -Preview -PlayMode`. The generator saves the interactive scene first, then creates a separate check scene. A generated-project-only runtime component drives the sample's controls and observes real Bird solvers and Unity objects across frames. Fresh result/exit verification and the runner's three-minute timeout remain in force. These tests are not shipped inside the package sample.

The first actual Unity 2020.3.33f1 run verified cursor/radius/selection/loss/recovery behavior but failed cleanup: removing the component left its camera and marker geometry alive. Fixed ownership by placing generated objects beneath a dedicated root and destroying only that root plus the owned materials. Unrelated authored children are preserved.

After the fix, all 58 Play Mode checks passed (exit 0): known sphere fitting, two separate cursors, cursor/ray synchronization, opening-induced range increase, selection size and single press edges, held positions and exactly one release per hand on pose loss, input-marker visibility, unpressed recovery, collider removal, and cleanup of camera/geometry/materials without deleting unrelated children. Whitespace check passed. Production solver/factory code was unchanged; the separate 75 editor assertions were not rerun.

Limits: headless Play Mode, not rendered visual review or actual GUI input events. No SDK-enabled tracking, VRChat or physical-device validation. No headset access, GUI launch retry or editor-install attempt. Next: visual review or optional backend assembly separation, with platform and modern-editor gates still explicit.
