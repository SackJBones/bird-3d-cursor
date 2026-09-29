# Pond garden corner

Garden01 gives the pond's broad inland terrace a small place to pause: two
unequal, angled low seats and three unequal planting groups. It follows Dana's
plain white architectural reference and the critic's request for purposeful
occupation of the empty deck. The open center remains available for standing
visitors and Bird gestures. Water and distant sightlines remain in front of the
seats; planting stays behind and beside them.

## Ordinary Unity authoring

`Assets/BirdWorld/CoastalGarden/Pond garden corner.prefab` is a conventional
nested prefab in the lower-water region. Seat and planter transforms, materials,
meshes and colliders remain editable in the Inspector. The editor-only **Bird >
Coastal world > Add pond garden once** command creates that first version and
refuses to overwrite an existing one. Do not regenerate the world to adjust it.
The first-candidate refinement moves the short seat and its planter one metre
inward, preserving asset identities; it refuses to run on later edited layouts.

Seven original saved meshes provide rounded white plinths, warm seat surfaces,
low backs, oval planters, inset soil and two forms of folded foliage. The leaves
use the existing opaque coastal vertex-color shader with explicit linear color
channels. No alpha cards, wind animation, added runtime scripts, network streams
or realtime lights are needed. The plinths and planters use existing architectural
materials and ordinary static MeshColliders; leaves have no collision.

The seat surface is 45 cm above the terrace. These are seating geometry only:
this pass does not add `VRCStation` seating or an interaction binding. The warm
surfaces connect this outdoor pocket to the cozy rooms. Broad low foliage and
one taller silhouette provide asymmetry without a repeated hedge or ring.

New architectural receivers have independent UV2 charts and participate in the
existing baked-lighting workflow. Leaf colors are painted facets; the foliage
does not add shadow-casting or lightmap cost. Explicitly rebake after moving
furniture or changing its display geometry. Existing pond geometry, guardrails,
fish, stairs, beacons and Bird control/presentation are preserved.

## Validation

`Invoke-UnityCoastalWorld.ps1 -CheckGarden` checks furniture support against the
actual pond-floor collider, approach/body clearance at three heights, flat seat
height and torso clearance, seated water sightlines through the real bank and
rails, finite mesh data, UV2, opaque materials and a bounded geometry budget.
It renders both approaches, the conversation corner, two seated views, a free
seaward overview and plan. The overview is not a supported visitor stance;
the original inland high view was floor-occluded and is only a rejected control.
The normal-frame walker includes both new approaches outward
and back alongside all existing routes. The unchanged outer pond circuit and
beacon stances still receive their own checks.

The ordinary SDK inspection export uses a temporary supported standing position
at the garden approach when this prefab exists, then restores the source scene
exactly. Load the normal arrival export again after inspecting it on the Quest.
Actual client rendering, compiled geometry checks, independent aesthetic review,
physical comfort and multiplayer remain distinct evidence. Read the newest
checkpoint and curated review for completed results and remaining findings.

All assets in this pass are original; no third-party art or license dependency
was introduced. This is a small garden addition, not a claim that the whole world
has reached its aesthetic or hangout-quality target.
