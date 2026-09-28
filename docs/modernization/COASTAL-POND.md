# Curved coastal pond and promenade

Pond01 reshapes the narrow rectangular fish study into a basin that sweeps
around the lower terrace. Its water, coping and outer bank share a curve; the
outer walk returns to the existing lower promenade. The broad inland terrace
remains useful for standing and talking. The stairs, tide room, higher terraces,
teleport beacons and Bird input/presentation are independent of this addition.

The current profile reserves a 3.3 m walking band, leaving about 3.2 m between
the visible rail bars. Water is 0.6 m below the floor. The new geometry totals
9,938 triangles including invisible guard meshes and the closed slab underside.
There are no added runtime scripts, lights or network streams.

## Conventional authoring

`Assets/BirdWorld/CoastalPond` contains an ordinary nested prefab, seven saved
mesh assets and an editor-only `Pond profile.asset`. It uses existing stone,
plaster and water materials. The profile supplies plan control points, water
half-width, walk width and floor/water levels. An explicit **Bird > Coastal world
> Update pond meshes only** command tessellates the profile and updates those
named mesh assets without replacing their GUIDs, prefab transforms or unrelated
objects. The world does not run a spline generator at runtime. A profile change
requires review of its joins, routes and unchanged beacon positions; the command
does not promise automatic circulation or landing relocation.

The one-time **Add curved pond once** migration replaces the original local
rectangular pieces with inactive scene overrides. Their original prefab objects
remain available for reference. It leaves the rest of the complex intact.
Do not rerun that migration over the saved pond. The old still fish studies are
distributed along the curved water; these are not schooling fish. Responsive
boids for local/remote logical Bird points remain a separate runtime task.

Walk and water top surfaces are separately triangulated. The old overlook,
rectangular pool and bordering slabs are inactive, preventing coplanar overlap.
The new floor meets the retained promenade with its existing small level change.
Visible rail bars have exact mesh pointing proxies on layer 17. Continuous
invisible player boundaries use layer 2, retaining the established separation
between pointing through rail gaps and walking protection. No physics collision
matrix change is needed. Water lies below the walking floor and does not provide
an invisible walkable lid. Saved avatar clicks and teleport actions remain off.

## Validation and review

Use `tests/Invoke-UnityCoastalWorld.ps1 -CheckPond` for actual collider support,
standing clearance across the winding route, retained landing points, water
separation and rendered views. It writes an additional complete-circuit route
for `-Walk`, which uses normal-frame `CharacterController.Move` outward and back.
The walker also pushes both SDK player layers against the new pond and sea
boundaries. `-CheckBeacons` separately exercises the existing targeting and
landing contract against the changed surroundings. Rebake saved lighting after
geometry changes, then run normal platform builds and a useful Quest inspection.
The existing `-BuildVistaInspection` export faces the curved basin when it is
present, using the same checked overlook footing and exact source restoration.
Deploy the normal arrival bundle again after that separate inspection.

See the newest checkpoint for actual results, numerical independent review and
device evidence. Editor tests do not establish VR comfort, physical finger-click
validation or real multiplayer. Keep rejected visual candidates and remaining
findings explicit. Original project geometry only; no third-party art acquired.

The focused collider check samples 319 positions; the normal-frame walker adds
the complete pond circuit to the existing routes and tests both SDK player
layers against both new safety boundaries. Curated evidence is in the heavy
repository, `Reference/WorldBuildingReviews/20260928-Pond01`. The first tight
curve self-intersected and was rejected before scene mutation. The accepted
broader sweep and cap-following terrace remove that fold and the initial
crescent-shaped join gaps. The original high-lookout floor-occlusion image is
retained as a control, with a supported beacon stance used for sightline review.

The independent critic accepts this bounded layout, retaining full-world scores
of 6.5 aesthetics / 8 navigability / 7 hangout suitability / 6.5 overall. The
near-concentric sweep needs purposeful bank-width variation, a stopping bay and
sparse planting in later passes. Exposed rail ends, alternating rail shading
and the unsupported-looking seaward slab remain limitations. Saved lighting has
two non-directional maps, 219 probes and 177 receivers; 141 UV-overlap warnings
remain. No claim of the overall >=8 design target is made.

The general world editability fixture previously regenerated original meshes
without restoring their later-authored UV2 channels. This cycle caught that
side effect in the asset diff, restored the exact baseline assets, and changed
the fixture to restore and assert complete mesh bytes and refresh collider
cooking. Intermediate exports made before restoration were never deployed.
