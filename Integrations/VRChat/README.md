# VRChat hand-data feasibility probe

`BirdHandDataProbe.cs` is a separate UdonSharp diagnostic for a Worlds SDK project. It does not adapt or enable the ordinary Bird MonoBehaviour package in VRChat, and does not implement sphere fitting or clicking.

It samples the local avatar's wrist and fifteen finger bones per hand at 10 Hz. Optional marker transforms show available positions, and a world-space UI Text label reports availability counts and VR mode. It does not synchronize data or change the player. Zero/near-zero and nonfinite positions are hidden; zero is the API's missing-bone sentinel, so a real bone at world origin is ambiguous. Counts are availability diagnostics, not tracking confidence. All marker objects should start inactive and have no physics colliders.

These are avatar bone origins, affected by the avatar and IK. Distal bones are not fingertip endpoints. VR mode is not evidence that hands are being tracked. Calibration, scale, avatar changes, controller poses and tracking loss need independent investigation before supplying these values to Bird. [VRChat's player-position API](https://creators.vrchat.com/worlds/udon/players/player-positions/) documents these distinctions.

## Reproduce in BirdWorld

Restore the heavy repository's BirdWorld VPM packages (Worlds/Base 3.10.5), then copy this source **and its meta file** to `BirdWorld/Assets/BirdGenerated/Runtime/`. Preserve the script GUID: the tracked Udon program asset references it. Copy `tests/UnityVRChatProbeChecks.cs` from this repository into `BirdWorld/Assets/BirdGenerated/Editor/` when generating a new probe scene. Use Unity 2022.3.22f1 in batch mode with `-executeMethod UnityVRChatProbeChecks.Run`.

The generator creates the program asset explicitly, invokes the real UdonSharp compiler, checks for compilation errors and retrievable program data, wires 32 collider-free markers, and saves a separate `BirdHandProbe.unity` scene. It refuses to overwrite that scene. The generator and runtime source remain authoritative here; the heavy project tracks scene/program assets and ignores the generated source copies. On a fresh checkout, restore the source/meta before opening the scene, then compile UdonSharp to regenerate serialized bytecode.

Compiler success alone does not prove execution in ClientSim, VRChat or a headset. Those are subsequent checks.


## Explicit pause and resume

Call the Udon custom events PauseProbe and ResumeProbe while the probe object remains active. Pause stops sampling and clears all marker flags/counters; resume requests a fresh sample. Actual ClientSim execution verified pause/hold/resume with the default desktop avatar (16/16 bones per hand). Automatic GameObject-disable cleanup remains under investigation: the handler compiles and works when dispatched directly, but the lifecycle test does not reliably receive it. Do not depend on deactivation alone to clear the independent status label; use PauseProbe before deactivating, and ResumeProbe after reactivating a paused probe. This is an explicit command path, not a claim that native lifecycle delivery is fixed.


Controlled missing-avatar recovery also passes in ClientSim: UnityClientSimProbeChecks.RunMissingBones temporarily removes the local simulator avatar animator reference, verifies SDK zero positions for all 32 sampled bones and Udon marker/count/label clearing, then restores the reference and verifies 16/16 availability per hand. This SDK-specific runtime fixture saves no scene changes and does not establish actual avatar-switch, selective missing-bone, scale or physical tracking-loss behavior. See the lightweight tests README for reproduction.


ClientSim scaling also passes via UnityClientSimProbeChecks.RunScale: runtime eye height 1.9 -> 0.95 -> 2.85 -> 1.9 m, all 32 Udon markers matching SDK bone positions within 2 mm, wrist-relative lengths scaling/recovering, and counts remaining 16/16 per hand. This proves probe following on the simulator avatar; it does not establish solver calibration, real avatar changes or hardware tracking. Original eye height is restored and no scene/global preferences are saved.


## Udon sphere fitter

BirdSphereFit.cs is a separate local-only UdonSharp algebraic least-squares sphere fitter. Set points (4-32 Vector3 values), send the Fit custom event, and read fitValid, center and radius. Every rejected fit clears outputs; callers must check fitValid before using them. Zero is a legitimate geometric point here: callers must remove/reject missing avatar-bone sentinels before fitting. Input selection is deliberately separate from the diagnostic's 32 bone origins, which are not the original Bird hand adapter's 16 fit points or true fingertip endpoints.

The fitter centers/scales the data, solves the symmetric 3x3 covariance system, and recovers radius from mean squared distance. This is the same algebraic objective as the original centered 4x4 fit, with an intentional normalized determinant guard (<= 1e-6) rejecting underdetermined/near-planar sets. The threshold is provisional; validation on realistic open-hand poses is still required. It uses bounded loops and no per-fit collections. Udon performance has not been profiled. No cursor range mapping, filtering, click hysteresis, avatar calibration or scene integration is included yet.

Copy the source and stable meta to Assets/BirdGenerated/Runtime in BirdWorld. Copy tests/UnityUdonSphereFitChecks.cs to the same runtime directory (it is editor-only), then run Unity 2022.3.22f1 with -batchmode -nographics -executeMethod UnityUdonSphereFitChecks.Run. The helper creates an ignored program asset, compiles real Udon, adds a temporary unsaved test object to BirdFeasibility, enters ClientSim, and sets input/reads output through the Udon heap and SendCustomEvent. Result: udon-sphere-result.txt. It does not call the C# proxy Fit method.


## Caller-fed cursor state

BirdCursorState.cs composes one dedicated BirdSphereFit with caller-supplied fit points, handRoot, indexTip and tracking. Send Step once per input sample, then consume poseValid, position, selected, down and up. Pulses last until the next Step or Cancel, not necessarily one Unity frame. Each cursor requires its own fitter; an optional cursorVisual is positioned/shown for valid samples and hidden on rejection. Invalid input cancels selection with one up pulse and holds the last finite position behind poseValid=false. Send Cancel before disabling/deactivating; no automatic lifecycle cleanup is claimed.

The component preserves Bird.cs's unfiltered range law and its nearest-of-fit-center-or-cursor selection sphere, with strict press depth >7 mm and release depth <5 mm. The caller provides the hand root (the ordinary Bird uses a weighted thumb/index base), and must supply a real or clearly synthetic index tip. This first Udon state component deliberately has no Kalman filter, twist, input adapter, avatar-scale normalization, networking or automatic sampling. It is not yet equivalent to the full filtered Bird runtime and does not turn avatar distal bones into fingertips. Extreme but finite ranges are not clamped; realistic-pose calibration and performance/usability testing remain required.

For compiled-VM validation, restore both cursor/fitter sources and stable metas plus tests/UnityUdonCursorChecks.cs into Assets/BirdGenerated/Runtime. Run Unity 2022.3.22f1 -batchmode -nographics -executeMethod UnityUdonCursorChecks.Run. The helper creates ignored program assets and two unsaved cursor/fitter instances, then sends real Udon Step/Cancel events with synthetic samples. Read udon-cursor-result.txt. Marker checks inspect transforms and active flags; they are not rendered-image or hardware validation.


## Synthetic world scene

BirdSyntheticDemo.cs feeds an animated tetrahedral pose and index-tip depth into one cursor/fitter pair at up to ~33 samples/s. It is a labeled demonstration, not a hand input adapter. Two instances produce cyan/pink paths with gold pressed cursors and click-count labels. Each LineRenderer retains at most 64 positions (128 total), tapers toward its oldest point and replaces old positions as new samples arrive. This is sample-count-bounded history, not a guaranteed wall-clock fade under slow frames. PauseDemo cancels the cursor, clears its trail and changes its label; ResumeDemo resumes with fresh history. Send pause before deactivation.

The heavy repository tracks BirdSyntheticDemo.unity, its three Udon program assets and simple Unlit/Color materials. Restore BirdSphereFit, BirdCursorState and BirdSyntheticDemo sources/metas into Assets/BirdGenerated/Runtime before opening/compiling a fresh checkout. To reproduce a new scene, copy tests/UnityUdonDemoChecks.cs into the same directory and run Generate (refuses an existing scene/program asset). Validate reopens the saved scene, runs ClientSim, checks bounded trails/clicks/pause/recovery and saves a camera image under the ignored Validation/UdonDemo directory. Use Unity 2022.3.22f1 -batchmode without -nographics for Validate. Neither command uploads a world or uses a headset.
