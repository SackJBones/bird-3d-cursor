# Dana's next world and interaction priorities

Received 2026-09-28 during the social-presentation cycle. This supplements the
initial request and architectural references. Dana tried Personal Bird 01 in
VRChat and reports that it feels like magic. The nearby working volume and the
current visual inflation are accepted; **do not change either while addressing
mid-range instability**. Follow the normal two-hour rhythm, roughly 30–60 minutes
of active work per cycle. Finish bounded, tested checkpoints. Functionality and
infrastructure take priority over decorative additions. Dana's subsequent
instruction explicitly dedicates the headset to this effort: use it extensively
for real VRChat build validation and replace its loaded build at any time. Do
not wait for permission or preserve an older scene merely because it is running.
Record actual device evidence; distinguish unattended checks from physical feel
and from real multi-client tests. An unavailable headset still must not block
independent development.

## Latest world additions

Lighting01, Vista01, Pond01 and Fish01 add baked architectural lighting, daylight
sky/coast, a curved pond circuit and bounded Bird-responsive cosmetic shoals.
Garden01 adds a small editable seating/planting corner on the inland terrace;
see `COASTAL-GARDEN.md` and the newest checkpoint for its completed validation.
Support01 adds one editable curved terrace bracket and seals the submerged
pond-wall gap; see `COASTAL-SUPPORT.md` for retained limitations and evidence.
The independent critic's full-world target remains unmet. Favor one bounded
pond rail shading/terminal-end refinement next, or physical interaction/social acceptance
when the necessary hands/clients are available. Do not repeat one-time authoring
migrations over the saved world. Retain the accepted Bird control and visual
sizing, and keep unvalidated click/teleport actions disabled.

## Earlier progress through R06

Social streams, contrasting hand pairs and body-departure acquisition debounce
are implemented and have synthetic coverage; actual multi-client acceptance
remains open. The mid-range investigation rejected earlier full filter influence
because its modest noise benefit worsened turning response; production feel
and visual sizing remain unchanged. See `MID-RANGE-ASSESSMENT.md`.

R06 repairs the pit's missing riser, circle joins and open seating ends, closes
the white rear-wall crown, and adds a rock enclosure without moving the complex.
Both platform builds and all 17 out-and-back walking routes pass; the world is
visibly loaded in Quest VRChat. See `COASTAL-WORLD-R06.md` and its independent
review. Enclosure is functional, but broader rock shaping and world aesthetics
remain unfinished (overall 6.5/10).

Next favor a bounded functional pass toward validated finger clicks and Bird
teleport beacons, or supported real-client social verification if its prerequisites
are available. Lack of actual hand motion or a second logged-in client must not
be relabeled as acceptance or stall independent preparation. Keep unvalidated
click actions disabled. Later coast/pond/lighting work can continue using the
critic's remaining design findings; do not repeat the rejected filter retune.

## Ordered work and proposed approach

The beacon targeting preview is described in `TELEPORT-BEACONS.md`. It provides
five editable destinations and a travel implementation behind an explicit gate;
saved-world clicking remains disabled. The newest checkpoint distinguishes
compiled fixtures, actual device loading and the outstanding physical click
validation. Shared cosmetic hover is also separate from this local preview.

1. **Social Bird and reliable acquisition.** Independent per-player snapshot
   streams, visible cursors/trails, lifecycle/late-join handling and distinct
   player palettes. Keep network interpolation out of the owner's control path.
   Debounce the pedestal: collecting Bird cannot immediately toggle it off;
   require leaving the vicinity with the body, then returning deliberately.
   Text should say to return here to put it away. Disabling is a minor activity.
   The implementation's synthetic network verification is clearly distinguished
   from a real two-client test.
   Dana's follow-up explicitly requires contrasting companion colors for the two
   hands (cyan/pink, lime/lavender, etc.), as well as varying the pair by player.
   Similar shades on both hands are not acceptable: hand identity is especially
   important. Preserve this in any later chosen-color/palette UI.
2. **Mid-range stability and validated clicks.** Reproduce the room-to-two-room
   range instability with the current avatar pipeline. Measure sphere-center
   noise, range-law derivative and filtering separately at fixed mean ranges,
   with wrist turns and hand closing as responsiveness constraints. Prefer a
   principled, bounded adjustment using existing sphere quantities. Preserve the
   accepted near geometry/dynamics and all size/visual inflation parameters.
   Keep the original Vector3 Kalman baseline, SE(3) symmetry checks and source
   sphere-direction aim. Do not add an independent openness measure. Validate
   pointer-finger clicking from near-fist through distant pointing before
   enabling it in the main world; avatar estimates alone do not establish this.
3. **Architectural repairs.** Patch conversation-pit holes with actual continuous
   walking collision and visible surfaces. Adjust the cliff geometry/location
   so the arrival cavern is enclosed by rock from outside views. Preserve the
   complex's existing architecture, routes and sightlines. Check front, rear,
   side and overhead views plus standing-capsule routes.
4. **Bird teleport beacons.** Editable vertical torus-shaped beacons, with safe
   landing transforms beside them. Use Bird point-through hover/highlight and
   validated pointer-finger click, with cancellation on input loss and no stale
   click replay. The local player teleports, not the beacon. Consider who sees
   shared cosmetic highlights separately from per-user targeting. Test floor,
   headroom, walls, varied avatar heights and useful cross-level sightlines.
   Retain the existing deliberate acquisition/point-through principles rather
   than letting an unrelated distant sweep grab every foreground object.
5. **Coast, garden and sky.** Add economical coast detail, distant land, plants
   and a fitting skybox. Match the white, matte architecture and approved
   references. Free assets may be used when their licenses fit; preserve source,
   license, author and required attribution. Use original geometry when simpler.
   Continue independent critic-agent review for aesthetics, navigability,
   hangout suitability and general VRChat quality; seek at least 8/10 on each
   over iterations, without confusing that review with headset acceptance.
6. **Curved pond and winding route.** Curve both water and outer pond edge,
   continue the pond around more of the complex, and extend the waterside path
   in a winding form. Prefer authored spline/control-point parameters and
   ordinary baked meshes/colliders. Keep furniture, walkable margins and social
   spaces usable; rerun traversal and sightline checks when changing geometry.
7. **Schooling fish.** Bounded, inexpensive boids that form groups and approach
   any valid Bird whose logical point is actually within the water volume.
   Include remote Birds through the social layer; the distant render proxy must
   never attract fish. Determine multiplayer authority/deterministic cosmetic
   behavior, update budget and timeout/cancellation before adding many fish.
   Pool visuals; test several simultaneous visitors and Quest frame cost.
8. **Reach goal: organic shells.** Investigate metaballs/metashapes, particularly
   ovoids and cones, marching cubes, decimation and baked lighting. Generate
   editable *shells around* the existing useful architecture, not a redesign.
   Begin with one small offline editor prototype and compare silhouettes,
   thickness, door clearance, UV/lightmap quality, mesh budget and colliders.
   Keep inexpensive collision separate from display geometry. Use the archived
   metaballs conversation and the existing independent design critic workflow.

One cycle should normally finish one coherent item or sub-item, with appropriate
tests, honest limitations, documentation and commits in both repositories when
both change. Adjust ordering when a real defect or feedback justifies it. Do not
expand this into one oversized implementation or let optional modeling research
block the useful interaction/world pipeline.

## Interaction validation aid

The separate [finger-tap practice display](CLICK-PRACTICE.md) reads existing
click geometry with all action gates off. It prepares physical near/far and
near-fist click feedback without holding independent coast/pond/sky work.
See the newest CHECKPOINT for completed tests and the actual Quest build.

## Cross-platform lighting follow-up

Beacons02 review exposed a Windows cavern-lighting weakness: hard sun shadows
flatten the white interior; the all-camera-layers control is equally flat, while
a temporary no-sun-shadow render restores contours. Actual Quest arrival stays
bright. Address economical interior fill/baked lighting in a bounded future
pass, retaining authored geometry and standard SDK quality settings. Keep the
normal and diagnostic platform captures distinct; see TELEPORT-BEACONS.md.

## Vista01 progress (2026-09-28)

The first bounded coast/sky pass is implemented and checked in real Quest VRChat:
an editable daylight sky, open sea and two receding landforms, preserving the
existing architecture and Bird control. See [COASTAL-VISTA.md](COASTAL-VISTA.md)
and the newest checkpoint for builds, device evidence and numerical review.
Both platform exports and focused captures pass. Full-world scores are now
6.5 aesthetics / 8 navigability / 7 hangout / 6.5 overall, still below the goal.
Original coast silhouettes need less regular shoulders and shoreline shapes.
Plants, curved pond/outer bank and winding route, then bounded social fish,
remain useful next passes. Lighting01 already addressed the broad Windows
interior shadow problem above; UV overlap/seam cleanup is still outstanding.
Do not repeat one-time world, lighting or vista migrations on the saved scene.

## Pond01 progress (2026-09-28)

The curved basin, curved outer bank and extended waterside circuit are now
ordinary editable assets, with a broad return to the existing lower promenade.
See [COASTAL-POND.md](COASTAL-POND.md) and the newest checkpoint for final platform
checks and actual device evidence. The old still fish are redistributed along
the water; they are not boids. Bird control, sizing and companion colors remain
unchanged. The independent critic accepts this bounded layout but retains the
6.5 / 8 / 7 / 6.5 full-world scores. Next favor restrained planting and one useful
stopping bay, or bounded social fish; avoid adding decorative curve noise.
Rail shading, the abrupt west rail end/join and the unsupported-looking seaward
overhang remain visible follow-ups. Physical clicks and multiplayer still need
their own acceptance and must not block independent work.

## Fish01 progress (2026-09-29)

Four six-fish cosmetic shoals now replace the still diamond fish. They patrol
the curved channel and gather around submerged local or remote logical Bird
points, releasing on withdrawal or invalid/expired tracking. Each client runs
its own bounded cosmetic simulation; no new synchronization stream is added.
See [POND-FISH.md](POND-FISH.md), the newest checkpoint and curated Fish01 review
for compiled tests, platform exports, actual Quest checks and remaining limits.
The independent critic accepts the revised loose gathering, while retaining
the 6.5 / 8 / 7 / 6.5 full-world scores. A first overlapping wreath was rejected.
The shaders, fish mesh and materials are original editable assets.

Next favor restrained planting and a useful stopping bay, including convincing
support and refinement of the exposed west rail join. Preserve the accepted
Bird geometry/dynamics, visual inflation and companion hand colors. Physical
Bird attraction, pointer-finger clicks, real multiplayer and occupied-world
Quest cost still require their own evidence. Do not hold independent world work
for missing hands or a second client.

## Verbatim request

> absolutely awesome. it feels like magic. in coming cycles, please "debounce" the obtainment of the bird so you have to physically leave the vicinity of the pedestal before returning there will disable it, just have the text reference returning here; it's currently too easy to end up in a random state of bird-enabled-vs-disabled and disabling bird is not a major activity here. please patch holes in the conversation pit, add some coast details and some faraway land to look at (use free assets that match this aesthetic if that's helpful), add plants, add a skybox, make the outer edge of the pond/fish area curved as well as the water pool itself, also curved, and have it extend around bit more of the complex, so the path that runs along it currently is continued in a winding way; add fish to the pond and make them boids that will make groups come toward any bird in the water; please make sure the cavern embedded in the cliff face where you spawn is indeed inside the rock at least to outside appearances, and I think since this is going so well the best way to do that is just to adjust the location/geometry of the cliff face rather than try to change the complex; add vertical-donut-shaped teleportation beacons you can select and use at a distance with the bird, they should highlight/visibly activate whenever someone "rolls over" them (that is, points-through them with their bird) and clicking them with the bird (using the pointer finger) should cause the player to jump next to the beacon (I'm excited for fun sightlines within the architecture that let you jump levels by getting in the right spot). please try to make the mid-range of motion more usable--right now the working volume is perfect, the mid-range is too touchy and it's very hard to get the bird to move in a controlled way that doesn't violently punch in and out when it's about a room or two's distance away (the range where the bird inflates visually is perfect though, please don't touch that), and investigate metaballs/metashapes (ovoids and cones seem very useful for this) plus marching cubes plus decimation plus baked lighting as a reach goal--the idea with those would be to add aesthetic organic-looking shells over the good work you've already done or will have done by that point, not to redesign the architecture. please note these down along with any plans you might have as to how to do them, and just do the normal cycle rhythm, adjusting plans as needed, don't bite off more than will be feasible in a half hour to an hour's work (which is, for you, probably 1-3 days' work for a person) at a time every 2 hours, I just want to make sure you know my vision so you don't run out of things to do. (very good point making sure other people can see your birds and trails. also probably colors should not always be the same for everyone, or we risk confusion. if there are other mechanistic or infrastructural things like that to address, do please continue prioritizing those & get functionality where it needs to be before indulging the more whimsical aspects of the above.) thanks so much for your work on this; I'm going to bed, see you in maybe a lot of cycles.
