# Coastal vista

Vista01 adds a quiet daytime sky and two receding coastal landforms beyond the
existing bounded island. The first island remains the distinct, nearer remote
interaction setting. The farther coast provides a low saddle, unequal shoulders
and an off-center crest, with lighter, cooler colors suggesting distance. Open
water remains a large part of the view. This is scenery, independent of Bird's
point geometry, ranges, filtering and presentation.

## Ordinary authoring assets

`Assets/BirdWorld/CoastalVista` contains a nested prefab, three mesh assets, one
vertex-color material, an ordinary skybox material with a simple gradient shader and an
editor-only profile. The prefab is added under the existing coastal region;
none of the six region roots, architecture or walking colliders is recreated.
The original finite rectangular sea is hidden by a saved scene override and
retained for editing/history. The replacement sea has a broad color gradient
and extends beyond the farther land, within the existing 10 km camera range.
This visual choice does not limit the logical Bird point.

Move or rotate the two landforms with ordinary prefab transforms. Edit their
cross sections and colors in `Coast profile.asset`, then explicitly run
**Bird > Coastal world > Update vista profile meshes only**. Each Vector4 stores
longitudinal position, lateral center, half-width and crest height in metres.
The command updates only the three named mesh assets, preserving their GUIDs,
prefab transforms and material edits. It does not regenerate the world. The
one-time **Add coastal vista** command refuses to overwrite an existing prefab.

The broad silhouettes and sea use 864 triangles altogether.
An opaque vertex-color shader supplies their authored atmospheric colors with
no textures, transparency, shadows or per-frame generator. There are no new
colliders, lights, Udon programs or network streams. The sky interpolates three
editable colors by vertical view direction, without textures, a sun disk or a
runtime light dependency. Flat ambient and existing baked lights stay at their
saved settings; rebake after changing the sky so the ordinary reflection
environment also matches it. Coast/sea Inspector colors are interpreted as sRGB
and explicitly converted to linear mesh color channels for the VRChat project.

The first candidate used Unity's standard [procedural sky controls](https://docs.unity3d.com/2022.3/Documentation/Manual/shader-skybox-procedural.html).
Its actual render read too dark against the terrain, so review replaced it with
the simpler explicitly authored gradient. This is a visual-design choice, not
a claim that the built-in shader is generally incompatible.
VRChat's [Android content limits](https://creators.vrchat.com/platforms/android/quest-content-limitations/)
distinguish world shaders from restricted avatar shaders. Successful shader
compilation is followed by normal SDK exports and an actual Quest view; it is
not treated as proof of final rendering quality or all-world performance.

## Scope and evidence

The runner's `-AddVista` performs the one-time authoring. `-RefineVista` only
migrates the rejected first procedural-sky candidate; do not rerun it on the
current assets. `-UpdateVistaMeshes` applies intentional profile edits.
`-CheckVista` runs a
focused Play Mode inventory and eight normal-quality view captures. It checks
the reference camera's sky, bounded geometry, finite/upward-facing triangles,
supported shaders, retained baked maps and absence of added collision/lights.
Use `-Platform Both -CheckVista -Build` for both normal target builds. The
latest checkpoint records actual results; fixed images do not establish headset
comfort, physical gestures, active Bird performance or multiplayer correctness.

`-Platform Android -BuildVistaInspection` makes a separately named normal SDK
bundle whose only source-scene difference is an initial spawn on the water
overlook. It first checks floor and standing clearance, then runs the same SDK
validation, export audit and size gates. A private source backup is written
before the temporary change; the exact production scene bytes are restored
after export, refusing to overwrite unexpected concurrent edits. The comparison
allows the normal SDK to permute its descriptor's `DynamicMaterials` list only;
membership and every other scene field must match the temporary scene. This permits
an unattended Quest to inspect the actual vista without pretending to control
hands or enabling teleport actions. Deploy the normal arrival build afterward.
If Unity crashes during this special export, compare the source against its
ignored `Validation/CoastalWorld/vista-inspection-source-backup.unity` before
continuing. Inspection and normal bundles have distinct names/results/hashes.

Review the view from the water overlook, high lookout and seated tide room, plus
arrival and beacon sightlines. Retain independent numerical review and unresolved
findings. This pass leaves the curved pond/path extension, shoreline planting,
schooling fish and broader architectural refinement to later bounded cycles.

## Independent review

The critic rejected the first dark-sky/pale-coast relationship, which suggested
twilight and snow, and could barely distinguish the second distance layer.
The revised daylight gradient, corrected vertex-color conversion and lateral
placement of the far coast restore that hierarchy. The critic recommends
retaining the bounded pass: arrival remains architecture/pickup first, open
water remains generous, and the seated-room view is calmer and deeper.

Full-world scores advance to **6.5 aesthetics / 8 navigability / 7 hangout /
6.5 overall**. The target of 8 in every category is still unmet. Near islands
remain blunt capped forms; distant ridges have overly clean triangular
silhouettes, and the sea/horizon remain austere. Later shoreline shaping should
add unequal shoulders and indentations before decorative density. Existing
large cliff planes and stacked architectural disks still constrain the score.

View 08 is an inboard floor-occlusion control, not proof of a beacon sightline.
View 06 exposes an existing doubled/mirrored beacon label from the back/side;
record it for a separate UI polish pass. Final Windows arrival, overlook and
seated-room renders match Android's material hierarchy in the critic's review.
This establishes editor-render parity; the latest checkpoint separately records
exports and actual Quest evidence. It does not establish PC-client rendering,
gestures, comfort or multiplayer acceptance.

## Credits

Coast/sea geometry, profile data, the vertex-color shader and gradient sky are
original project work. No third-party art package, photograph or texture was
downloaded. Dana's archived concept conversation and four images remain design
references, not runtime art assets.
