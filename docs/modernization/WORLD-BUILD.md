# Local world-bundle validation

This check uses Unity 2022.3.22f1 and Worlds SDK 3.10.5 to build the
experimental avatar-preview scene into a Windows world bundle. It does not
upload a world, launch VRChat, or validate multiplayer or headset behavior.

Restore BirdWorld's VPM dependencies and all integration C# sources with their
metas into `Assets/BirdGenerated/Runtime`, as described in the integration and
test READMEs. Copy `tests/UnityWorldBundleChecks.cs` into
`Assets/BirdGenerated/Editor` (it belongs in the Editor folder).

Run Unity against BirdWorld with `-batchmode -buildTarget Win64`, an explicit
`-logFile`, and one of these `-executeMethod` entry points. Do not run two editor
processes against the same project. Each method exits Unity itself.

1. `UnityWorldSdkSetup.Run` applies the SDK's active-platform scripting defines.
   Restart the editor afterward so its Worlds editor assembly can compile.
2. `UnityWorldSdkSetup.RunLayers` applies the SDK's required layer names and
   collision matrix only when its checks report a mismatch. Review the resulting
   project-settings diff; custom layer assignments may need adjustment.
3. `UnityWorldBundleChecks.Run` opens the authored preview and saves a generated
   copy before SDK preprocessing. It compiles Udon, uses the SDK's normal
   `VRCSdkControlPanelWorldBuilder` and public build-only `Build()` API, and
   preserves validation failures in `world-bundle-result.txt`.

The similarly named `VRCSdkControlPanelWorldBuilderV3` is gated by the legacy
`V3SdkUI.V3Enabled()` switch and is not the applicable builder in this SDK.
The helper does not override that switch or suppress SDK validation.

A successful run copies the nonempty bundle to the ignored heavy-repository
`Validation/WorldBuild/BirdAvatarPreview.vrcw` and records its size and SHA256.
`PENDING`, an absent result, or a failed process is not success. The editor-update
watchdog allows four minutes after the entry point starts, but cannot interrupt
a blocked synchronous operation; an external runner must enforce a wall-clock
limit if needed.

The SDK may configure project settings and migrate XR settings during a build.
Review Git status afterward and preserve any pre-existing untracked XR assets.
The helper restores the shader-stripping EditorPref it touches; it does not
promise that every SDK preference or generated cache is unchanged. Build
success is separate from scene interaction, upload, and physical-input evidence.
