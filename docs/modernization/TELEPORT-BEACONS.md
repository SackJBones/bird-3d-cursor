# Bird teleport beacons

The first coastal-world integration adds five editable vertical rings: arrival,
conversation terrace, upper terrace, high lookout and water garden. Acquire Bird
normally at the pedestal, then point through a ring to highlight it. The saved
world is a **targeting preview**: avatar finger clicks and `allowTeleport` remain
off. Do not present this as completed physical click-to-travel acceptance.

## Components and intent

`BirdTeleportBeacon` owns a circular local-XY target, an ordinary landing
Transform, clearance settings and renderer colors. `BirdTeleportRouter` owns
local targeting, fresh release/press contact, arbitration and locomotion.
`BirdAvatarUiInput` submits the accepted logical Bird point after avatar IK to
a dedicated `BirdUiPointer`; neither visual inflation nor the distant rendering
shell affects the interaction. Bird geometry and filtering are unchanged.

The saved `Assets/BirdWorld/TeleportBeacons/Bird travel network.prefab` contains
the network, rings and internal references. Only references to the existing
personal station and two hand sources are scene overrides. World builders can
move a beacon and its child landing in the Inspector, change the material/colors,
or replace its renderer. The rings have no colliders and do not block walking.
`BirdTeleportAuthoring.Add` is a one-time additive authoring operation, not a
regenerator to run over subsequent manual edits. It waits for normal SDK program
asset initialization before serializing new Udon behaviours.

## Targeting and activation

The finite segment from the palm to the **logical** Bird point must cross the
target disk. Shorter points cannot target a beacon behind them. Solid Default
and Environment geometry occludes the segment; triggers and players do not.
A palm inside a solid collider is rejected. Closest intersected beacon wins;
earlier UI consumption wins over travel. This is an explicit point-through UI
policy, separate from the inside-to-outside contact policy of spherical scrolling.

Hover is local and has idle, cyan-highlight and amber-blocked states. It adds no
network traffic. Shared cosmetic rollover cues can be a later presentation
layer; remote cursor streaming remains independent.

Travel requires a released, fresh hover on the same beacon followed by a fresh
press. Tracking loss, stale samples, source/identity change, target change,
put-away and permission changes cancel arming. Both hands share a short cooldown
and can request at most one trip per processing pass. Travel clears both cursor
and pointer histories. A held click cannot replay after the cooldown.

Each action rechecks support at nine points around the standing footprint and
a clear capsule above it. Bad floor height, sloping/absent/trigger support,
ledges, side walls and insufficient headroom reject the destination. Headroom is
at least 1.75 m or avatar eye height plus 0.25 m. Heights above 8 m are rejected,
not silently squeezed into a room. These are conservative checks, not a proof
against every arbitrary dynamic collider arrangement or sub-sample floor hole.

The gated action uses normal local-player `TeleportTo`, preserves facing, and
clears velocity. VRChat only allows local-player teleportation and a station
may prevent it, so the diagnostic counter records **requests**, not guaranteed
arrivals. See the official [player position API](https://creators.vrchat.com/worlds/udon/players/player-positions/).
Avatar eye height is used for visual headroom; VRChat avatar scaling does not
change world collision. See [avatar scaling](https://creators.vrchat.com/worlds/udon/players/player-avatar-scaling/).

## Validation and remaining work

`Invoke-UnityCoastalWorld.ps1 -CheckBeacons` runs the compiled Udon programs in
ClientSim, including authored destinations/sightlines, transformed disks,
occlusion, dynamic floor/wall/headroom fixtures, input lifecycle, both-hand
arbitration and normal local TeleportTo with an explicit test permission. It
renders saved-world views and actual ring states. `-CheckBird` also checks the
same-frame avatar-to-pointer binding while preserving the saved Lab 14 settings.
The saved permission remains off; test permission changes are runtime-only.

Actual Quest loading/rendering, synthetic activation tests, physical finger
clicking and multi-client behavior are distinct evidence. See the latest
CHECKPOINT for completed results and device state. `BirdCursorState.clickDepth`
is available even while clicks are disabled; a later bounded diagnostic can
measure its response without enabling locomotion. Validate physical near-fist
through distant clicks before enabling the saved travel gate. Do not hold up
independent coast/pond/sky work for unavailable hand motion.

## Independent presentation review, 2026-09-28

The first pass was not accepted: arrival overlapped the pickup sculpture,
upper rings were tiny arcs behind slab/rail silhouettes, and the downward water
target was obscured. Arrival moved beside the threshold onto a clear flat
landing; upper/high rings rose 1.5 m without moving their landings, the upper
ring turned toward the water approach, and the water ring moved to the outer
water walk. Architecture and guardrails remain intact.

The critic accepts the revised **gated targeting preview**. Addition-only
scores are 6.5 aesthetics, 5.5 discoverability/navigation, 7 hangout fit and
6 preview usability. Full-world R06 scores stay 6/8/6.5/6.5; the target of at
least 8 in each category is unmet. Arrival is distinct, upper targets show
complete outlines, and the actual hover render is clearly brighter.

The return link is still limited: the water target is partly rail-obscured
from a raised-hand near-edge stance and invisible from the captured inboard
stance. Passing a point-through segment at that edge is not evidence of
comfortable ordinary palm-height travel. A later placement iteration should
expose the full water ring from a supported lookout stance, keeping its landing
safe and inboard. Do not remove the guardrail to manufacture a sightline.

Compiled Android and Windows fixtures each pass 70 assertions over 100 frames
in this revision, with process exit 0.
The authored water-to-upper/high sightlines use a 1.2 m palm height above the
walk; the return test explicitly uses a raised 1.65 m hand near the lookout edge.
All five landing areas pass. Saved-world avatar integration passes 550 assertions
over 298 Android frames and retains the Lab 14 scalar settings. Normal SDK
exports pass on both platforms. The Android bundle is transferred/hash-verified
and visibly running in Quest VRChat in inspected 15:07 and 15:10 UTC captures.
These device observations establish rendering, not real hand targeting/clicks.
See CHECKPOINT and heavy `Reference/TeleportBeacons/20260928` for hashes and
curated evidence; raw device data stays private.
