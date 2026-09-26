# Bird VRChat integration

The integration includes caller-fed geometry, experimental avatar input, and
local Udon menu/spherical interaction components. The latest authored example is
`BirdWorld/Assets/BirdWorld/Scenes/BirdUiDemo.unity` in the heavy repository.
It offers nested color selection, back-surface flick/coast, Back/Close and Reset.
Its labeled desktop input is a demonstration source; avatar-pose calibration and
physical hand input remain separate work.

See [Udon UI setup and contracts](../../docs/modernization/UI-UDON.md). Run
`tests/Invoke-UnityUdonUiChecks.ps1` to restore source/meta pairs and exercise the
compiled station in ClientSim; optional world builds use the SDK's build-only API.
Existing probe, synthetic-cursor and avatar-preview notes follow below.

## Hand-data feasibility probe

`BirdHandDataProbe.cs` is a separate UdonSharp diagnostic for a Worlds SDK project. It does not adapt or enable the ordinary Bird MonoBehaviour package in VRChat, and does not implement sphere fitting or clicking.

It samples the local avatar's wrist and fifteen finger bones per hand at 10 Hz. Optional marker transforms show available positions, and a world-space UI Text label reports availability counts and VR mode. It does not synchronize data or change the player. Zero/near-zero and nonfinite positions are hidden; zero is the API's missing-bone sentinel, so a real bone at world origin is ambiguous. Counts are availability diagnostics, not tracking confidence. All marker objects should start inactive and have no physics colliders.

These are avatar bone origins, affected by the avatar and IK. Distal bones are not fingertip endpoints. VR mode is not evidence that hands are being tracked. Calibration, scale, avatar changes, controller poses and tracking loss need independent investigation before supplying these values to Bird. [VRChat's player-position API](https://creators.vrchat.com/worlds/udon/players/player-positions/) documents these distinctions.

## Reproduce in BirdWorld

Restore the heavy repository's BirdWorld VPM packages (Worlds/Base 3.10.5), then copy this source **and its meta file** to `BirdWorld/Assets/BirdGenerated/Runtime/`. Preserve the script GUID: the tracked Udon program asset references it. Copy `tests/UnityVRChatProbeChecks.cs` from this repository into `BirdWorld/Assets/BirdGenerated/Editor/` when generating a new probe scene. Use Unity 2022.3.22f1 in batch mode with `-executeMethod UnityVRChatProbeChecks.Run`.

The generator creates the program asset explicitly, invokes the real UdonSharp compiler, checks for compilation errors and retrievable program data, wires 32 collider-free markers, and saves a separate `BirdHandProbe.unity` scene. It refuses to overwrite that scene. The generator and runtime source remain authoritative here; the heavy project tracks scene/program assets and ignores the generated source copies. On a fresh checkout, restore the source/meta before opening the scene, then compile UdonSharp to regenerate serialized bytecode.

Compiler success alone does not prove execution in ClientSim, VRChat or a headset. Those are subsequent checks.


## Explicit pause and resume

Call the Udon custom events PauseProbe and ResumeProbe while the probe object remains active. Pause stops sampling and clears all marker flags/counters; resume requests a fresh sample. Actual ClientSim execution verified pause/hold/resume with the default desktop avatar (16/16 bones per hand). Automatic GameObject and backing-component disable/re-enable also pass: OnDisable clears markers/counts/label, and Update rejects inactive/disabled execution so a queued SDK update cannot repopulate the cleared display. This probe-only lifecycle evidence does not establish cleanup for the cursor/input components or physical tracking.


Controlled missing-avatar recovery also passes in ClientSim: UnityClientSimProbeChecks.RunMissingBones temporarily removes the local simulator avatar animator reference, verifies SDK zero positions for all 32 sampled bones and Udon marker/count/label clearing, then restores the reference and verifies 16/16 availability per hand. This SDK-specific runtime fixture saves no scene changes and does not establish actual avatar-switch, selective missing-bone, scale or physical tracking-loss behavior. See the lightweight tests README for reproduction.


ClientSim scaling also passes via UnityClientSimProbeChecks.RunScale: runtime eye height 1.9 -> 0.95 -> 2.85 -> 1.9 m, all 32 Udon markers matching SDK bone positions within 2 mm, wrist-relative lengths scaling/recovering, and counts remaining 16/16 per hand. This proves probe following on the simulator avatar; it does not establish solver calibration, real avatar changes or hardware tracking. Original eye height is restored and no scene/global preferences are saved.


## Udon sphere fitter

BirdSphereFit.cs is a separate local-only UdonSharp algebraic least-squares sphere fitter. Set points (4-32 Vector3 values), send the Fit custom event, and read fitValid, center and radius. Every rejected fit clears outputs; callers must check fitValid before using them. Zero is a legitimate geometric point here: callers must remove/reject missing avatar-bone sentinels before fitting. Input selection is deliberately separate from the diagnostic's 32 bone origins, which are not the original Bird hand adapter's 16 fit points or true fingertip endpoints.

The fitter centers/scales the data, solves the symmetric 3x3 covariance system, and recovers radius from mean squared distance. This is the same algebraic objective as the original centered 4x4 fit, with an intentional normalized determinant guard (<= 1e-6) rejecting underdetermined/near-planar sets. The threshold is provisional; validation on realistic open-hand poses is still required. It uses bounded loops and no per-fit collections. Udon performance has not been profiled. No cursor range mapping, filtering, click hysteresis, avatar calibration or scene integration is included yet.

Copy the source and stable meta to Assets/BirdGenerated/Runtime in BirdWorld. Copy tests/UnityUdonSphereFitChecks.cs to the same runtime directory (it is editor-only), then run Unity 2022.3.22f1 with -batchmode -nographics -executeMethod UnityUdonSphereFitChecks.Run. The helper creates an ignored program asset, compiles real Udon, adds a temporary unsaved test object to BirdFeasibility, enters ClientSim, and sets input/reads output through the Udon heap and SendCustomEvent. Result: udon-sphere-result.txt. It does not call the C# proxy Fit method.


## Caller-fed cursor state

BirdCursorState.cs composes one dedicated BirdSphereFit with caller-supplied fit points, handRoot, indexTip and tracking. Send Step once per input sample, then consume poseValid, position, selected, down and up. Pulses last until the next Step or Cancel, not necessarily one Unity frame. Each cursor requires its own fitter; an optional cursorVisual is positioned/shown for valid samples and hidden on rejection. Invalid input cancels selection with one up pulse and holds the last finite position behind poseValid=false. Send Cancel before disabling/deactivating; no automatic lifecycle cleanup is claimed.

The component preserves Bird.cs's unfiltered range law and its nearest-of-fit-center-or-cursor selection sphere, with strict press depth >7 mm and release depth <5 mm. The caller provides the hand root (the ordinary Bird uses a weighted thumb/index base), and must supply a real or clearly synthetic index tip. The Udon state component has optional Kalman smoothing (see below), but no twist, input adapter, avatar-scale normalization, networking or automatic sampling. It is not equivalent to the full Bird runtime and does not turn avatar distal bones into fingertips. Extreme but finite ranges are not clamped; realistic-pose calibration and performance/usability testing remain required.

For compiled-VM validation, restore both cursor/fitter sources and stable metas plus tests/UnityUdonCursorChecks.cs into Assets/BirdGenerated/Runtime. Run Unity 2022.3.22f1 -batchmode -nographics -executeMethod UnityUdonCursorChecks.Run. The helper creates ignored program assets and two unsaved cursor/fitter instances, then sends real Udon Step/Cancel events with synthetic samples. Read udon-cursor-result.txt. Marker checks inspect transforms and active flags; they are not rendered-image or hardware validation.


## Synthetic world scene

BirdSyntheticDemo.cs feeds an animated tetrahedral pose and index-tip depth into one cursor/fitter pair at up to ~33 samples/s. It is a labeled demonstration, not a hand input adapter. Two instances produce cyan/pink paths with gold pressed cursors and click-count labels. Each LineRenderer retains at most 64 positions (128 total), tapers toward its oldest point and replaces old positions as new samples arrive. This is sample-count-bounded history, not a guaranteed wall-clock fade under slow frames. PauseDemo cancels the cursor, clears its trail and changes its label; ResumeDemo resumes with fresh history. Send pause before deactivation.

The heavy repository tracks BirdSyntheticDemo.unity, its three Udon program assets and simple Unlit/Color materials. Restore BirdSphereFit, BirdCursorState and BirdSyntheticDemo sources/metas into Assets/BirdGenerated/Runtime before opening/compiling a fresh checkout. To reproduce a new scene, copy tests/UnityUdonDemoChecks.cs into the same directory and run Generate (refuses an existing scene/program asset). Validate reopens the saved scene, runs ClientSim, checks bounded trails/clicks/pause/recovery and saves a camera image under the ignored Validation/UdonDemo directory. Use Unity 2022.3.22f1 -batchmode without -nographics for Validate. Neither command uploads a world or uses a headset.


## Conventional demo controls

The saved synthetic scene includes local PAUSE/RESUME and CLEAR TRAILS buttons using BirdDemoControl.Interact, BoxColliders and 3 m interaction proximity. Pause cancels both cursors and clears trails; resume restarts sampling. ClearTrail empties history without changing click counts or pause state, so running demos begin new trails on their next sample. Controls affect only the local client and require no Bird input. The pause button owns its toggle state; external code should not independently pause the demo behind it.

Fresh checkouts must now restore all four source/meta pairs: BirdSphereFit, BirdCursorState, BirdSyntheticDemo and BirdDemoControl. For a new scene, run UnityUdonDemoChecks.Generate, then AddControls, then Validate in separate editor runs. AddControls upgrades the existing scene once, refuses duplicate controls or a mismatched program source, and reuses matching assets left by an interrupted upgrade. Validate dispatches compiled _interact events and checks both demos, control labels, colliders, clearing and recovery. That is not a pointer/controller hit-test or a claim of in-headset usability.


## Optional cursor smoothing

BirdCursorState.smoothing enables a scalar-covariance Vector3 Kalman update with Bird's existing Q=0.001 and R=270*d^3, where d is fitted-center distance from hand root. rawPosition exposes the unfiltered range result; position and cursor-centered click testing use the filtered result. No additional per-sample collections are allocated. This is a per-sample filter, not a frame-rate-normalized one.

Unlike the legacy zero-initialized filter, the first valid sample seeds directly at its measurement. Invalid input, tracking loss and Cancel discard filter history; the next valid pose seeds immediately. Switching smoothing off bypasses the filter, and switching it back on also reseeds. The last finite position remains held behind poseValid=false during loss. This intentional recovery policy still needs physical comfort and tracking-quality validation.

For cursor checks, also copy Unity/BirdPlugin/Runtime/Scripts/KalmanFilterVector3.cs into the ignored generated runtime folder as the ordinary C# reference used by the test helper. Production Udon does not depend on that class. UnityUdonDemoChecks.EnableSmoothing updates only the two saved cursor smoothing fields and saves the scene; Validate then checks the scene's runtime and control behavior with rendering enabled.


## Experimental avatar-bone input (pointer only)

BirdAvatarInput feeds a dedicated cursor/fitter pair from the local VRCPlayerApi at up to ~33 Hz. Configure rightHand per instance. Its 12 fit points are thumb intermediate/distal, index proximal, and proximal/intermediate/distal of middle/ring/little fingers. It omits four fingertip samples from ordinary Bird's 16-point fit. The weighted root uses 60% index proximal plus 40% thumb proximal. Index distal is supplied only as a finite placeholder; it is not treated as a fingertip, and the adapter forces cursor.clicksAllowed=false on every sample.

All 14 required bone origins (12 fit points, thumb proximal and index distal) must be finite and nonzero. Missing data cancels the cursor and clears availability state; valid recovery restarts fitting. A bone at world origin remains ambiguous with the SDK's missing sentinel. dataReady/available describe bone availability, not tracking confidence or fit quality. poseValid separately reports the solver's acceptance. Optional text labels identify the approximation and disabled clicks. Each adapter must own its cursor/fitter; do not attach a synthetic driver to the same cursor. Explicitly cancel before external deactivation.

This experimental adapter is not wired into the authored synthetic scene. No inferred fingertips, scale normalization, real-avatar calibration, gesture fidelity, performance or physical hand tracking is claimed. Avatar IK/controller/desktop poses may produce available data and accepted fits without tracked fingers. The existing range law is highly nonlinear; raw avatar-origin geometry may give poor ranges and must be evaluated before an end-user mode is enabled.

Restore BirdAvatarInput source/meta and tests/UnityAvatarInputChecks.cs into the generated runtime folder, with cursor/fitter sources and their program assets available. Run UnityAvatarInputChecks.Run in Unity 2022.3.22f1 batchmode/nographics. It creates an ignored adapter program and unsaved left/right instances, observes actual ClientSim/Udon Update, independently checks bone-name mapping and weighted roots, then temporarily removes/restores the simulator avatar reference to test cancellation/recovery. It never calls the adapter's C# Update. Result: udon-avatar-result.txt.


Observed limitation: the default desktop ClientSim avatar produces accepted 12-point fits but about 1,250 m cursor ranges under the unchanged Bird range law. The experimental adapter therefore rejects ranges above maximumPreviewRange (default 3 m), cancels/hides the cursor and exposes rangeRejected/measuredRange plus a calibration label. This is an adapter-only preview guard, not final range calibration or a restriction on ordinary Bird/synthetic cursor range. Never interpret bone availability or a finite fit as a usable pointing mode. The adapter is intentionally not enabled in the authored demo.


Scale diagnosis: RunScaleCalibration verifies both hands at 0.5x/1x/1.5x and restored size. Default-avatar raw ranges are about 20 m / 1250 m / 14220 m; hand-span normalization stabilizes the baseline but leaves it unusable. Compiled fits agree with the original 4x4 equations within 0.2 mm. See ../../docs/modernization/AVATAR-CALIBRATION.md and its CSV for measured geometry, range-law analysis and the next calibration gate. No production normalization was selected from this one pose.


## Opt-in neutral preview calibration

After valid bone data has arrived, send CalibrateNeutral to BirdAvatarInput to anchor the current pose to neutralPreviewRange (default 0.3 m, finite and >0, at most 3 m and maximumPreviewRange). The fixed 24-iteration inversion finds the corresponding input to Bird's original monotonic range law. Calibration stores fitted-center distance divided by the sum of middle-proximal/intermediate and intermediate/distal segment lengths; this length measure is less bend-dependent than the diagnostic straight-line span. Each sample compensates for current hand size and applies that calibrated distance ratio to the range law. Direction and the nonlinear range shape remain intact.

BirdCursorState.rangeDistanceMultiplier defaults to 1 and affects both the range-law input and distance-dependent filter noise. Raw target range is anchored in world meters; filtering can lag while the hand/root moves. Existing synthetic/default cursor mapping is unchanged. The adapter still disables clicks and retains its excessive-preview-range rejection.

Calibration is explicit and runtime-only. ResetCalibration cancels the cursor and restores multiplier 1. Missing required bone data, a handedness change, or a local OnAvatarChanged callback clears calibration. It does not automatically resume a previous calibration after loss. The avatar-change callback compiles, but actual avatar-swap event delivery and physical tracking-loss ergonomics remain unvalidated. Calibration needs re-evaluation across multiple real poses/avatars; this adapter is still outside the authored scene.

Run UnityAvatarInputChecks.RunNeutralPreview to test the 0.3 m raw target across default-avatar 1x/0.5x/1.5x scale, missing-avatar invalidation, invalid target rejection, explicit recalibration and reset. Original eye height is restored afterward. The single simulator pose cannot establish hand-gesture fidelity or comfort.


Controlled articulation fixture: copy both UnityAvatarInputChecks.cs and UnityAvatarPoseFixture.cs into the generated runtime folder. RunPoseVariation exercises neutral, +/-15 degree local-Z proximal finger rotations and restored neutral via actual ClientSim bone APIs and compiled Udon. The fixture saves/restores runtime joint rotations and Animator enable state without modifying SDK assets. Passing these limited perturbations does not establish human open/close gesture semantics, avatar-swap fidelity or hand tracking.


## Separate avatar-preview scene

The heavy BirdWorld project now has an experimental BirdAvatarPreview scene with two bone-input/cursor/fitter chains, per-hand status labels, and conventional CALIBRATE/RESET controls. This is separate from BirdSyntheticDemo. Preview inputs set requireCalibration=true so their markers remain hidden before calibration and after reset, even if an uncalibrated avatar would otherwise fit within the preview range guard. Defaults for existing standalone adapter fixtures remain unchanged. Both cursor components are serialized with clicks disabled.

BirdAvatarControl dispatches CalibrateNeutral or ResetCalibration to both inputs and reports success/incomplete calibration. Controls are local, use ordinary collider-based Interact at 3 m proximity, and require no Bird clicking. The original synthetic materials are reused. Physical activation and actual avatar fidelity remain unvalidated; this scene is a diagnostic preview, not a complete Bird World release.

Restore BirdSphereFit, BirdCursorState, BirdAvatarInput and BirdAvatarControl source/meta pairs into Assets/BirdGenerated/Runtime before opening a fresh checkout of this scene. Other authored scenes need their own sources too. UnityAvatarSceneChecks.Generate creates the scene and promotes the ignored adapter program asset into the tracked Programs directory, preserving its GUID. It refuses to overwrite an existing scene and checks program source references. Validate separately reopens it, compiles Udon and exercises calibration/reset/recalibration through compiled Interact dispatch. Run Validate without -nographics for the camera capture under Validation/AvatarPreview. The capture isolates authored visuals from simulator avatar/UI without changing global preferences.


## Optional pose-aware limits (v0.5)

The previous opt-in sphere cap is withdrawn after physical feedback. BirdSphereFit remains an unconstrained solver and reports a conditioning confidence. BirdCursorState.useHandLimits (default false) requires the canonical 16 points, original weighted root and verified palm-outward normal. It preserves ordinary fits, blends a separate flat-hand point law, and returns the point to the root for a classified full fist. This is an experimental geometric policy, separate from presentation. The avatar adapter does not enable it. See [hand limits](../../docs/modernization/HAND-LIMITS.md) for inputs, thresholds, migration from the removed fitter fields and synthetic-versus-physical validation. UnityUdonCursorChecks now requires UnityPalmFitChecks in the generated runtime directory.
