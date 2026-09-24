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
