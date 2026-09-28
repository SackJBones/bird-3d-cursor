# Coastal world baked daylight

The previous Windows world lost the cavern's curved forms under the sun's
realtime shadows. Quest's disabled realtime shadows made it brighter, but flat.
The current lighting pass targets bright neutral plaster with soft depth on
both platforms, preserving the saved architecture and accepted Bird controls.
Check the latest checkpoint for actual validation and deployment status.

## Editable Unity assets

The scene uses ordinary `LightingSettings`, baked lights, lightmap textures and
a `LightProbeGroup`. The additional area lights and visitor probes live under
`06 Experience anchors / Baked coastal daylight`. Existing room fills and the
sun are baked. No custom runtime lighting component is needed.

The refined budget is 6 texels per metre, non-directional maps no larger than
1024 pixels, modest samples and four indirect bounces. Distant landscape receives
a much lower lightmap scale. Bounced light supplies contact depth; additional
ambient occlusion was removed after the first review found dirty scalloped joins.
Hidden walking guards do not contribute to GI. Probes provide baked light for moving visitors.
Actual atlas counts and memory should be assessed from each completed bake.

Six curved shell renderers use an ordinary `Curved shell rays` Lightmap Parameters
asset with a 2 cm ray-origin offset. This removes most of the faceted self-shadow
scallops at their joins. Floors and stairs retain the default parameters; their
contact shading must not be sacrificed to hide shell artifacts. See Unity's
[bake ray-origin offset](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/LightmapParameters-pushoff.html).

This follows the official [VRChat Android lighting guidance](https://creators.vrchat.com/platforms/android/quest-content-optimization/)
to prefer baked illumination and probes over realtime lights. Unity's
[LightingSettings API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/LightingSettings.html)
and [baked ambient occlusion documentation](https://docs.unity3d.com/2022.3/Documentation/Manual/LightingBakedAmbientOcclusion.html)
describe the ordinary controls used here. No third-party assets were acquired.

## Iteration and validation

Preparation is a one-time migration, available through the Bird editor menu or
the runner's `-PrepareLighting` switch. It refuses to replace an existing setup.
`-RefineLighting` advances only the original 3-texel setup through the first
critic refinement; it also refuses an already-refined scene. Neither migration
should be rerun over the checked-in result.
`-SmoothLightingJoins` is the separate one-time installation of the six shell
overrides. Afterward, edit the saved parameters asset through its Inspector.
Edit the saved lights, probe positions and lighting asset with Unity's normal
tools. `-BakeLighting` bakes the existing saved scene; `-CheckLighting` separately
opens Play Mode under the platform's normal quality settings, verifies texture
and probe bounds, and renders eight review views. The runner requires both a
PASS result and a successful process exit.

```powershell
./tests/Invoke-UnityCoastalWorld.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/BirdWorld' `
  -Platform Both -BakeLighting -CheckLighting -Build
```

Profile geometry edits invalidate baked lighting and may clear secondary UVs.
Check UV2 before rebaking; retain the saved mesh asset identity and verify its
surface/collider shape. The migration uses Unity's
[per-triangle UV generator](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unwrapping.GeneratePerTriangleUV.html)
and assigns only UV2 to the existing separate triangle corners. Unlike direct
secondary-UV generation, this avoids welding even nearly coincident seam
positions. Existing positions, normals, triangle indices and bounds are checked
against the committed baseline. A successful static render does not establish active
Quest frame cost, avatar lighting, hand gestures or multiplayer acceptance.
Inspect a real Quest VRChat deployment as part of each material lighting change.

The separate older Windows combined navigation/capture shutdown failure is
documented in [COASTAL-WORLD.md](COASTAL-WORLD.md). Focused lighting checks must
stand on their own results, not overwrite that limitation.

## Independent visual review, 2026-09-28

The first 3-texel candidate recovered depth but overexposed the pickup and showed
dirty-looking seams. Higher sampling, larger chart margins, no extra AO and
gentler fill recovered the pedestal/sculpture detail and improved lounge light.
The targeted shell offset then removed most of the scalloped canopy edge. The
critic recommends retaining this candidate and found no new obvious floating
structure or broad light leak in the arrival, support, stair-return and passage
frames. This is a limited image review, not exhaustive geometry or headset acceptance.

Lighting-only scores are 6.5 aesthetics, 6.5 white readability, 7 navigation
hierarchy and 7 hangout suitability. Full-world scores remain unchanged; the
8/10 targets remain unmet. Support mottling, faint shell seams, slightly warm
whites and weak contrast on downward stairs remain. UV-overlap warnings also
remain in the low-density bake; this is not a warning-free final art pass.
The latest checkpoint records final platform/export/device outcomes separately.

The critic also checked normal Windows VRC High views against Android. The old
flat dark-shadow failure is materially resolved: the opening, stairs, sculpture
and support retain their shapes, and both targets share the same broad visual
hierarchy. This is editor-render parity, not actual PC-client acceptance.
