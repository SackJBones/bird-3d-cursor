# Editable coastal world blockout

`BirdWorld/Assets/BirdWorld/Scenes/BirdCoastalWorld.unity` in the heavy repository
is a separate architectural scene following [WORLD-DESIGN.md](WORLD-DESIGN.md)
and the four approved references. It does not replace `BirdTrackingLab.unity`.
The tracking lab remains the place to evaluate Bird input while this world is
developed. The personal Bird station now occupies the arrival pedestal; see
[PERSONAL-BIRD.md](PERSONAL-BIRD.md) for behavior and the dated checkpoint for
validation/deployment. UI, Hanoi and mandala remain reserved experience anchors.

R06 repairs the conversation-pit openings and encloses the arrival in a saved
rock shoulder without moving the complex. See [COASTAL-WORLD-R06.md](COASTAL-WORLD-R06.md)
for scoped authoring/checks and [COASTAL-WORLD-REVIEW-R06.md](COASTAL-WORLD-REVIEW-R06.md)
for the independent critique. The broader world remains an architectural blockout.

## Authored structure

Six ordinary prefab instances separate arrival, social wings, supported coastal
terraces, lower water spaces, landscape and experience anchors. Meshes and
materials are saved assets. No runtime procedural generator is needed, and an
artist can edit prefab instances, overrides and individual objects normally.

The arrival cavern has a plain rear wall, a broad circular opening with shallow
steps, a folded sculpture marking the intended first-flight position, and two
side rooms. A branching column supports the stacked terraces. Upper and lower
cliff rooms, a sunken conversation pit, a fish-water overlook and a high lookout
provide seven route destinations. The steep ridge, sea, distant headland and
bounded island are self-authored placeholders for later landscape work.

Plaster is neutral white with low smoothness. Warm floors, sage cushions and
timber backdrops distinguish the seating spaces. The four room lights are
unshadowed vertex lights; their actual Quest cost and appearance still need
device measurement. Rendered shadow values are not the material's base color.

## Editing without replacing authored work

The maintained editor sources are in `Integrations/VRChat/Editor`; the runner
copies them into the restored project's generated editor directory.

* **Bird / Coastal world / Create authored world** creates an initial scene and
  six region prefabs. It refuses to overwrite an existing scene or prefab.
* **Bird / Coastal world / Update profile meshes only** reads the saved
  `CoastalWorld/WorldProfile.asset` and updates five shared meshes: arrival
  vault, circular threshold, branching support, upper slab and lookout slab.
  It preserves asset GUIDs/references, object transforms and authored additions,
  and refreshes open-scene mesh colliders. Invalid dimensions fail before edits.
* Floors, stairs, rails and experience anchors do **not** reflow when a profile
  dimension changes. Check their alignment after resizing. Destination/demo
  anchors are in a separate prefab and do not follow a relocated room.
* Material fields in the profile seed creation only. Edit the saved materials
  afterward. Make additions in ordinary prefab/scene objects; do not regenerate
  the whole world to change furniture or artwork.

The current stair and deck geometry is deliberately simple. The mesh tools are
scoped helpers rather than an architectural constraint solver. Presentation and
circulation should continue to evolve through small, reviewable authored edits.

### R05 scoped revision

The checked-in prefabs include the R05 doorway/tide-room revision. Arrival wings
now connect through circular bores with a shared radius and center height; the
flat ceiling beams and protruding floor strips are gone. The cliff shoulder owns
one opening, with the redundant inner portal retained but disabled. The tide
room faces along the coast, has L-shaped seating, and opens onto a short guarded
turn off the lower promenade. Its destination anchor moves with this revision.
Room vertex fill is reduced; white material values remain unchanged.

Independent review prompted a second local adjustment: matching portal/throat
tessellation and one continuous bore through both wall thicknesses removed the
bright dotted joins. The tide room turns 25 degrees coastward and shifts slightly
cliffward, opening its primary seated view onto water with a cliff edge to the
left. A wider guarded landing sits 2 cm below the timber floor. The old camera
12 is retained for comparison and no longer follows the rotated room's outward
axis; camera 16 is the current seated outlook and 17 shows approach/return.

`Bird / Coastal world / Apply R05 doorway and tide room revision once` is a
one-time migration for the earlier authored blockout, also exposed as
`-ReviseR05` by the runner. **Do not apply it to the current checked-in scene.**
It rejects an existing revision marker and reserved mesh assets, resolves all
target objects before mutations, and saves five existing prefab assets without
recreating the scene or replacing unrelated children. Existing scene overrides
remain overrides. The migration is scoped, not a general merge/transaction tool;
use version control before applying it to another edited copy. A newly created
baseline world can be revised with this explicit step. Future edits can use the
ordinary prefab objects and saved meshes directly.

The migration includes the independent-review refinement. `-RefineR05` exists
only to advance the reviewed first-pass R05 assets; both stages reject their
own completion markers. Do not combine `-ReviseR05` and `-RefineR05` or run either
against the finished checked-in revision. R05's two stages were executed and
checked separately during development.

## Reproduce checks and SDK exports

Use the maintained Unity 2022.3.22f1 project restored through the existing pinned
VPM workflow. This runner assumes the shared project/runtime sources have
already been restored; it is not a standalone replacement for project setup.
Close the editor before running:

```powershell
./tests/Invoke-UnityCoastalWorld.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/BirdWorld' `
  -Platform Android -Check -Walk -Build
```

Do not add `-Create` for the checked-in scene. Windows runs first and Android
last when `-Platform Both` is selected. Results and logs are written in the project; renders, routes and bundles
are under the heavy repository's ignored `Validation/CoastalWorld` directory.
Per-platform results and process exit codes are retained. `-Walk -Build` can run
independently of `-Check`, using its previously generated route file; use this
only for the same unchanged scene/collision geometry.

The scene check verifies spawn clearance, six prefab regions, all seven complete
NavMesh routes for a 1.75 m by 0.5 m standing capsule, and profile edit
preservation. Twenty views include room interiors looking back toward their
entrances, a seated tide-room view, lateral passages, an upper-floor view and a
circulation plan with roofs explicitly hidden. A sampled standing-capsule grid
checks longitudinal and lateral clearance and supporting floors across both
arrival-wing passages. The separate
Play Mode check uses real `CharacterController.Move` on normal frames against
saved colliders, walking each route out and back without a turnaround teleport.
It temporarily disables ClientSim's idle player driver/controller to isolate
world collision geometry; it does not replace colliders or bypass obstacles.

Builds use the unmodified VRChat SDK builder, its compressed and uncompressed
upload-size gates, an exported-scene component audit and bundle catalog/hash
checks. This script neither launches the headset nor uploads online. A private
upload still requires the normal SDK account/metadata/eligibility steps. The
same authored scene is the intended publication source.

These checks establish reproducible geometry and export evidence, not headset
comfort, avatar-size coverage, multiplayer behavior or measured Quest frame
cost. The current world is an architectural blockout, not a completed Bird
experience. See [independent review](COASTAL-WORLD-REVIEW.md) and the latest
[checkpoint](CHECKPOINT.md) for measured results and remaining work.

### Windows editor shutdown failure

The initial Windows `-Check` runs completed all scene assertions and fifteen
captures, then Unity 2022.3.22f1 crashed during shutdown with exit code
`-1073741819` (`0xC0000005`). The Windows event fault address resolves, using
the installed Unity symbols, to `JobQueue::WaitForJobGroupID + 0x6`. This is
not a successful process-level check. Its root cause remains unestablished.

Delaying exit, releasing the scene/unused assets, allowing normal editor updates
and replacing generic serialized mesh copying did not eliminate the failure.
The ineffective teardown workarounds were removed; mesh updates now use the
ordinary public Mesh channel API while preserving asset identity. The runner
continues to require both the written PASS result and exit code zero, and saves
the exit code even on failure. No SDK or validation gate is patched or ignored.

Run Windows `-Walk -Build` independently to assess those operations, and then
return to Android. Do not describe `-Platform Both -Check` as clean until the
Windows shutdown issue is resolved. Failed attempts remain in local ignored
`Validation/CoastalWorld/R04/windows-check-*-attempt` evidence. This limitation
does not establish a world runtime crash; actual client behavior is separate.

## Provenance

All blockout meshes, materials, furniture, foliage and fish were authored for
this project. No external asset package was downloaded for this checkpoint.
Unity built-in primitives and shaders are used. Existing SDK/package notices
remain under their own licenses. The four user-supplied images are architectural
references, not imported game textures; their provenance is preserved in the
heavy repository's `Reference/WorldDesign20260927/README.md`. Future acquired
assets must follow the attribution rules in WORLD-DESIGN.md.
