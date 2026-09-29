# Pond rail finish

Rail01 replaces the overlapping top-rail boxes with continuous square-section
sweeps along the existing pond and coast paths. The 0.10 m square section,
1.10 m overall height and prior post positions remain. Each genuinely exposed
end is capped and has a slim post directly beneath it; deliberate route gaps
remain open. This follows the architectural references' quiet rail outlines.

The existing two saved meshes and prefab remain ordinary editable Unity assets.
An editor-only `BirdCoastalRailMesh` builds joined surfaces with continuous
normals along each face and crisp cross-section corners. There are no internal
end faces at every sample. `Bird > Coastal world > Update pond rail meshes only`
updates the two rail assets, retaining GUIDs and prefab overrides. The original
finish migration runs once; do not replay it or regenerate the whole pond to
make a rail edit. Recheck sightlines and rebake after later mesh changes.

The rail renderers use the existing matte plaster. Their initial lightmap scale
rises from 0.3 to 4, leaving the world lighting settings intact. Unity documents
this as the renderer's relative UV size within the lightmap; the change requests
more lightmap pixels for these narrow bars, but is not a measurement of their
packed texel density. Actual packing, shadows and seams require baked review.
See the [Unity Mesh Renderer lightmapping reference](https://docs.unity3d.com/2022.3/Documentation/Manual/class-MeshRenderer.html#lightmapping).
Future explicit mesh updates preserve Inspector lighting choices instead of
silently resetting them.

The matching visible MeshColliders stay on the pointing layer. Independent
invisible player guards retain their exact mesh bytes. The finish operation
also verifies byte-identical floor, water and basin meshes. Focused checks cover
closed oriented edges, normals/UV2, dimensions, continuous top paths, all posts,
open spans, guard layers and exact visible proxies. Compiled beacon and normal
pond checks remain separate; synthetic pointers are not physical hand testing.

Comparison views cover the west approach, seated water, both sides of the west
terminal, supported high return, east walk and a free seaward camera. The
separate Android inspection export starts on the existing western promenade;
it adds no temporary footing in this revision and restores the source scene.
Always leave the actual Quest on the normal production arrival after inspecting.

No Bird fitting, range, filters, inflation, click law, complementary hand pairs,
network stream or runtime script changes are part of this pass. See the newest
checkpoint for retained candidates, lightmap warnings, actual device evidence
and the independent critic's scores and limitations.
