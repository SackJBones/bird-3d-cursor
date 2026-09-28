# Taking Bird with you

Research checked 2026-09-28 against official VRChat documentation. Recheck before
implementing a portable release: this area is changing.

## Product direction

Dana's intended journey is: visit Bird World, get Bird, learn it socially, and
enable it elsewhere whenever desired. At its simplest Bird is a little dot
that changes color when clicked, with a trail. Alternative embodiments could
be a mandala, a chosen-color fireball/trail for dancing, an inventory, a flying
paintbrush, or a pet that flies where Bird points. These are **roadmap concepts,
not authorization to implement each demo now**. Keep geometry independent of
embodiment and provide a coherent, current set of options for avatar and world
developers. The current deliverable is the personal pedestal in Bird World.

## Supported paths and their limits

| Path | What it can provide | Current decision |
| --- | --- | --- |
| World Udon prefab/package | Full current sphere fit, filters and avatar-bone adapter in any world whose creator installs it | Implement now; same geometric core, replaceable input and presentation |
| Native Items/Props | The desired avatar-independent inventory and cross-world spawning | Preferred future route, presently gated by creator access |
| Avatar integration | An attached visual/prop with expression toggles that accompanies the avatar | Bounded feasibility work later; do not assume our Udon code can run there |
| External solver + OSC avatar parameters | A companion can send values to a prepared avatar | Optional advanced route; requires an independently supported hand-data source and companion setup |

**Items really exist.** The current official FAQ describes portable content
independent of a particular avatar/world, inventory spawning and action-menu
shortcuts, but explicitly says user-created Items are not supported. Therefore
there is no supported public Bird Item upload workflow established by this
research. Do not call private endpoints, modify the client, or treat undocumented
API listings as permission to publish. [VRChat Items FAQ](https://help.vrchat.com/hc/en-us/articles/45059565610387-Items-FAQ)

Worlds and instances can restrict Props; recent client releases added controls
for Props that affect player movement. Even a future portable Bird should
respect host/user visibility and interaction policy rather than promise that
all embodiments work everywhere. [VRChat 2026.2.3](https://docs.vrchat.com/docs/vrchat-202623)

**World persistence does not transfer Bird.** PlayerData/PlayerObjects can save
preferences for return visits, but saved data cannot be shared between different
worlds. PlayerObjects are useful for per-player world instances and social
presentation, not a portable inventory of executable code.
[Persistence](https://creators.vrchat.com/worlds/udon/persistence/)

**Avatar authoring is a different runtime.** Only allowed avatar components run;
arbitrary custom scripts are excluded. Udon is the world scripting system.
An expression-controlled visual/constraint rig is plausible, but equivalence to
Bird's sphere fit and filters is unproven. We must measure any component-only
approximation instead of quietly substituting a wrist ray. Adopting a provided
Bird avatar would also replace the visitor's avatar; it does not install Bird
onto the avatar they already wear.
[Allowed avatar components](https://creators.vrchat.com/avatars/whitelisted-avatar-components/whitelisted-avatar-components/),
[Udon](https://creators.vrchat.com/worlds/udon/)

The newer `VRCRaycast` component supplies world/player intersection information
to avatars on supported platforms, with a documented 1000-unit maximum. It may
help a future avatar embodiment indicate hits; it does not supply Bird's sphere
fit, hand data processing or unbounded geometric range.
[Avatar Raycast](https://creators.vrchat.com/avatars/avatar-components/raycast/)

Quest avatar restrictions also matter: avatars must use SDK-provided shaders,
while world shaders have different allowances. The present renderer is not an avatar-ready
asset. Design and validate a separate mobile embodiment before promising parity.
[Android content limitations](https://creators.vrchat.com/platforms/android/quest-content-limitations/)

OSC can control avatar parameters, and official release notes explicitly include
PC and Quest support. That does not establish a raw per-joint hand stream or a
solver running inside Quest VRChat. Treat external hand acquisition, transport,
latency, avatar parameter precision and independent installation as feasibility
gates. This would not be the frictionless pedestal experience.
[OSC avatar parameters](https://docs.vrchat.com/docs/osc-avatar-parameters),
[VRChat 2022.1.1](https://docs.vrchat.com/docs/vrchat-202211)

## Distribution architecture

Keep one versioned geometric implementation with conformance fixtures. World
input reads avatar bones; other supported input adapters may read different
tracking sources. Interaction policies consume the point and lifecycle events.
Embodiments consume the result and may render a cursor, trail, object or nothing.
A mandala must not depend on a UI cursor prefab.

The recommended creator distribution is a VPM package/repository with explicit
SDK/platform compatibility, versions and changelog. Provide optional world,
avatar and future Item adapters only when each is validated. VCC supports custom
repositories/packages; updates are installed and rebuilt by creators. Do not
promise automatic remote updates to already-uploaded worlds/avatars or download
executable content at runtime.
[VPM packages](https://vcc.docs.vrchat.com/vpm/packages/)

Near-term: ship the reusable world prefab, test social visibility and clicks,
then publish a small documented package. Revisit public Item creation at the
portable milestone. If access opens, prototype the full input-to-point pipeline
there before committing to a port. Keep avatar/OSC investigations bounded and
separate so they cannot strand the VRChat world MVP.
