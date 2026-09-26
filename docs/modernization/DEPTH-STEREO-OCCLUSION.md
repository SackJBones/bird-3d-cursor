# Logical depth for the optional Bird renderer

## Version 0.8: world occlusion corrected

Dana's physical v0.7 report exposed the visible consequence of the measured
shell bug: Bird seemed unable to reach the mountains. The geometric point
already travels beyond them; the display previously tested depth at its
compressed position, always closer than 500 m. No range/filter/hand law was
changed to address this.

`BirdLogicalDepth.shader` keeps shell vertex positions for clipping/rasterization
and supplies logical fragment depth via `SV_Depth`. The core and locator use a
uniform logical/rendered distance ratio. Each pair of trail vertices carries its
own ratio in UV2. Perspective interpolation of `(1/ratio, renderedViewDepth)`,
then division in the fragment, gives screen-linear inverse logical depth across
a ribbon whose endpoints have different distances. Inverting Unity's
`LinearEyeDepth` handles both conventional and reversed depth. Points beyond
the world far plane use far depth: visible against sky, hidden by opaque world.
The core renders after opaque objects AND the skybox (queue 2501), before
transparent trail/locator layers. The sphere writes depth; the lines do not.

This is a Built-in pipeline, perspective-camera presentation policy, independent
of Bird geometry and interaction. It is not a general transparency solver;
transparent scene surfaces still follow Unity's normal sorting/depth behavior.
The shared shell retains its small binocular-position approximation. Huge
logical coordinates retain floating-point limitations. `SV_Depth` can reduce
early-depth GPU optimization; no headset performance claim follows from the
editor checks. Unity documents [fragment depth semantics](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-ShaderSemantics.html),
[depth parameters](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-UnityShaderVariables.html)
and the [single-pass instancing macros](https://docs.unity3d.com/2022.3/Documentation/Manual/SinglePassInstancing.html)
used here. The APK builder explicitly retains the shader.

Real Unity 2022.3.22f1 renders now pass on both Direct3D11 (reversed depth) and
OpenGLCore (conventional depth): 24 stereo configurations, 28 captures, 11
occlusion controls, **zero of five incorrect wall cases**. Additional controls
cover partial silhouette masking, a trail crossing a 600 m wall with endpoints
at 400/1000 m, a fully hidden far trail and a beyond-clip core against a skybox.
Near clip in the current fixture is 0.05 m. Maximum extra disparity remains
0.127686 px at 1024/60 degrees, identity through 100 m. These parallel mono-eye
cameras do not establish headset stereo comfort or actual OpenXR rendering.

The sections below preserve the earlier failing baseline; their v0.4 statements
are historical. Current evidence is saved with the v0.8 measurements.

---

# Shared display depth: stereo and occlusion probe

This checkpoint measures the current optional presentation. It does not change
Bird's logical point or repair world occlusion. The installed Quest app remains
v0.4; no headset access or APK rebuild was needed.

## Setup and evidence

`UnityDepthStereoChecks.Run` executes in Unity 2022.3.22f1 with an empty scene,
the real `BirdDepthVisual`, a 1024-square render target, 60-degree field of view,
4x MSAA, a 0.005 m near clip and a 2000 m far clip. Parallel mono cameras
represent two eyes. This is a controlled binocular geometry/rendering probe,
not OpenXR single-pass rendering or a physical headset stereo test.

It measures eight logical distances from 0.4 m to one billion meters at
58/64/72 mm eye spacing. Sixteen eye images use the 64 mm case. Seven further
images exercise opaque-wall occlusion and direct-world controls. Visible cyan
pixels and their weighted center are read back from the actual rendered image.
The small CSV measurements are preserved with this note; generated PNGs and
logs remain in the ignored heavy validation project.

## Stereo result

The display shell preserves the point through 100 m, then approaches 500 m.
With one shared projected point for both eyes, very distant points acquire
extra binocular disparity. The maximum observed excess was **0.127686 pixel**
across this probe's configurations. No added disparity was measured through
100 m, where the point is unchanged.

| Logical distance | Display distance | Extra disparity at 64 mm eye spacing |
| --- | ---: | ---: |
| 0.4-100 m | Unchanged | 0 px |
| 1000 m | 376.923 m | 0.093842 px |
| 1,000,000 m | 499.840 m | 0.113495 px |
| 1,000,000,000 m | 500.000 m | 0.113525 px |

These are matrix-derived pixel differences for this camera setup. Rendered
centroids were also checked within 0.6 pixel of their predicted display centers;
subpixel rasterization does not resolve the ideal disparity with arbitrary
precision. Device projection, resolution, late head updates and perception can
differ. A small numerical error is not proof of comfortable or convincing depth.

## Confirmed occlusion failure

| Logical point | Opaque wall | Should be visible? | Rendered visible? |
| ---: | ---: | :---: | :---: |
| 2 m | 1 m | No | No |
| 1000 m | 300 m | No | No |
| 1000 m | 600 m | No | **Yes** |
| 1000 m | 1500 m | Yes | Yes |
| 1,000,000 m | 600 m | No | **Yes** |

The 1000 m cursor renders at 377 m, in front of the 600 m wall. The wall and
cursor therefore behave correctly for their displayed coordinates but
incorrectly for Bird's logical coordinates. Eight cyan pixels survived in each
of the two wrong cases. A direct-world reference placed the solid marker at its
actual 1000 m position: the same wall hid it, and removing the wall revealed it.
Thus this is not a missing wall material, a clipped reference or a broken
occlusion test. Five control checks passed, while **two of the five logical
visibility cases remain wrong**.

The diagnostic reports `MEASURED` and explicitly states the limitation. Its
successful exit means the measurement and controls ran, not that world
occlusion passed. It must not be cited as a completed occlusion requirement.

## Next implementation boundary

Keep the geometric point and interaction ray in logical coordinates. A hit
indicator may mark an intersection independently; hiding a visual must not
silently clamp the point or change click policy.

For ordinary world-occluded presentation, next prototype a rendering path that
preserves logical depth for depth testing while keeping distant visuals inside
the camera's clip bounds. Verify opaque/partial occlusion, trails crossing the
depth transition, near size, stereo eyes and the Android graphics path before
replacing the current experimental shell.

An always-visible locator can be a deliberate experience option, visually
distinct from the solid point. It should not arise accidentally from using the
wrong depth. A binary ray query at the cursor center would not by itself solve
partial sphere occlusion or an entire trail crossing an obstacle. The host's
rendering/interaction policy remains downstream of Bird's geometry; mandalas
need not adopt UI visibility conventions.

Data: [stereo measurements](measurements/depth-stereo-2026-09-26.csv),
[occlusion measurements](measurements/depth-occlusion-2026-09-26.csv).
