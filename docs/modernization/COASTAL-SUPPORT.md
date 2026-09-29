# Pond terrace support

Support01 prototypes one broad swept bracket beneath the western part of the
pond terrace. Its root enters the coastal ridge and its upper end meets the
dry slab. This follows the reference's substantial white supports while keeping
the existing floor, basin, garden, routes and architecture in place. It is one
support study, not a complete redesign of the complex's structure.

`Assets/BirdWorld/CoastalSupport` contains an ordinary nested prefab, saved mesh
and editor-only `Support profile.asset`. The profile's sections specify height,
horizontal center and two radii. An explicit **Bird > Coastal world > Update
pond support mesh only** command lofts those sections offline, preserving the
mesh GUID and prefab transforms/materials. The initial **Add pond support once**
command refuses to overwrite existing assets. Later edits require renewed join,
basin and visual checks and an explicit lighting bake; they do not automatically
adapt to changes in the pond or cliff. Runtime content is a MeshFilter,
MeshRenderer and matching static MeshCollider, using the existing matte plaster.

The focused `-CheckSupport` verifies a closed consistently wound mesh, positive
volume, finite geometry and UV2, a bounded triangle count, all geometry below
the occupied floor, top attachment against the actual terrace collider and root
embedding against the actual ridge surface. Temporary exact-mesh probes check
the saved basin bed without changing terrain or water collision. Sampled shell
vertices and triangle centroids must remain below that bed wherever they cross
its footprint. The pond check also casts horizontal rays near the bottom of
both walls; these catch side gaps that the vertical clearance probes miss.
Pond, garden and lighting checks remain separate.

Comparison views include the prior seaward composition, low and oblique free
cameras, a wide exterior, the west approach and the seated water view. Free
cameras demonstrate silhouette and joins; they are not visitor standing spots.
Independent review, actual Quest rendering, compiled geometry checks and
physical comfort are distinct evidence. Read the newest checkpoint for final
results, retained/rejected candidates and remaining design limitations.

No Bird fitting, range, filtering, inflation, paired colors or interaction gates
are changed. This is original project geometry and introduces no third-party
art, scripts, network stream or realtime light.

Support01 also exposed a pre-existing submerged gap: the pond fascia stopped
8 cm below the water, but the fish bed was 55 cm below it. This let a diagonal
view see the white support through the open side of the basin even after
vertical bed-clearance tests passed. The explicit `-SealPondBed` repair extends
only those original wall-bottom vertices to 2 cm below the actual bed, retaining
all other vertices and triangle indices. Do not replay that one-time repair.
The pond profile now records `basinDepthBelowWater`; ordinary future mesh
updates use it, and newly authored fish beds take their depth from it. Keep
this profile value and the saved school depth consistent when editing.

The separate Android inspection export adds a temporary ordinary solid footing
outside the architecture to expose the support in the actual Quest client.
That footing is for inspection only, not a visitor route or part of production.
The exporter verifies its standing clearance, uses normal SDK validation, and
restores the production scene bytes after exporting. Always return the headset
to the production arrival build after inspecting the underside.

The independent critic retains the seven-section bracket and basin repair on
both Android and Windows. The cliff junction still reads somewhat applied,
and slab contact shading is uneven; this is a retained prototype, not final
architectural acceptance. The next suggested bounded pass is guardrail shading
and terminal-end cleanup without changing player containment or Bird sightlines.
