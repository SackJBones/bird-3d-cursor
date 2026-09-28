# Coastal R06: conversation pit and arrival enclosure

Dana requested holes in the conversation pit repaired and the arrival cavern
embedded in the cliff from outside. This is a scoped repair to the existing
complex. Rooms, stairs, experience anchors, personal/social Bird integration,
accepted near control and cursor inflation retain their layout and settings.

## Geometry

The old terrace ended at height 1 m while the first pit tread began at 0.8 m,
with no upper riser between them. All 192 sampled outward rays at height 0.9 m
missed the old rim. The terrace opening also used 96 circle chords while the
separate stairs used 64. Partial-ring seating pieces had uncapped ends and
undersides. These are geometry defects, independent of material shading.

One closed 96-segment stepped bowl now owns a 20 cm collar at terrace height,
the missing upper riser, four shallow treads, every remaining riser and the
underside. The terrace opening and existing timber floor use the same circle
sampling. The four superseded step objects remain inactive for inspection;
their replacements are ordinary saved MeshFilter/MeshRenderer/MeshCollider
components. The bench, cushion and back keep their arrangement, with their open
ends and bottoms closed. Visible collision-bearing surfaces use the same mesh
as their colliders. No invisible replacement floor hides the defect.

The plain arrival rear wall previously stopped at y=12.5 while its vault's
inner crown reaches y=13. A closed rear wall now follows the actual 48-chord
vault profile through its outer crown. It stays a plain white wall. This repairs
the white enclosure itself, separately from the new geology outside it.

A closed rock shoulder extends from the original long ridge around the cavern
and side-room roofs. Its front lip follows the room envelope, its outer face
slopes back into a larger ridge mass, and a real internal cavity keeps rock out
of the white rooms. Its underside reaches below sea level. The complex itself
has not been moved to achieve this enclosure. This is economical authored
geometry, not the proposed future metaball system, and does not complete the
broader coast, main architectural massing or lighting work.

The first enclosure was rejected after rendered review: its 70 m coastal crest
read as a giant flat screen. The revision uses a low coastal lip, broad facets
and a shoulder that rises while receding inland. Its hollow stays outside the
existing white rooms. This decision came from both direct image inspection and
the independent design critic, rather than treating closed-mesh checks as an
aesthetic pass.

## Editing and reproduction

`BirdCoastalWorldAuthoring.ApplyR06` is a one-time migration, available through
`Invoke-UnityCoastalWorld.ps1 -RepairR06`. **Do not rerun it on the saved R06.**
It checks expected objects/reserved meshes first, edits only the arrival,
terraces and landscape prefab assets, retains existing mesh GUIDs when changing
their geometry, and adds three named mesh assets. Other region prefabs and the
personal Bird prefab are untouched. Like R05, this is a scoped migration rather
than a transaction/merge tool for arbitrarily edited projects.

The resulting scene uses ordinary saved meshes and prefab instances; no runtime
generator is introduced. Future shape changes can edit those assets or use a
new explicitly scoped editor operation. The older five-mesh WorldProfile
updater does not regenerate this bowl, rear wall or rock shoulder; changing the
arrival dimensions requires checking these joins too. Do not regenerate the
world or replay old migrations over authored work.

`-RefineRockR06` updates only the named rock mesh, preserving its asset GUID and
the surrounding authored scene. It intentionally replaces that mesh's geometry;
do not use it to merge manual edits to the same mesh.

The scene check adds closed-edge/winding checks for seven repaired meshes,
dense rays around all pit risers/treads, roof/rear-wall coverage and sampled
standing-volume rock exclusion. New views show descent, seated use, low risers,
the return through the seat opening, and roof/east/rear/west exteriors. The
explicitly diagnostic circulation plan hides the new rock roof as well as the
architectural roofs; ordinary views never hide it.

The normal-frame CharacterController suite retains all seven destination
routes, and adds eight radial pit descents, a full inner-ring circuit, and a
route through the seating gap to the lowered floor. Each runs both directions
without a turnaround reset or collision bypass. These are sampled standing
geometry checks, not physical comfort or arbitrary-avatar coverage.

After the saved geometry changes, run Android `-Check -Walk -Build`, then
Windows `-Walk -Build`, and restore Android. The existing Windows combined
scene/capture shutdown defect remains a separate unresolved issue; do not
claim those combined checks passed. The scene inventory now recognizes the
specific Bird presentation trail filters that receive their mesh at runtime;
any other missing authored mesh still fails validation. Triangle/material
counts remain inventory, not measured Quest performance.

See the newest CHECKPOINT and the independent R06 review for exact completed
checks, build identities, device state, design scores and remaining limitations.
All new geometry uses existing self-authored materials and needs no additional
third-party asset attribution.
