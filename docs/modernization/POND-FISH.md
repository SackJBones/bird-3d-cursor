# Bird-responsive pond shoals

Fish01 is a cosmetic embodiment downstream of Bird's geometric point. Four
six-fish shoals patrol the curved pond and gather around valid Bird endpoints
inside its water volume. This does not change Bird fitting, filtering, range,
clicks, cursor inflation, acquisition or companion hand colors.

## Runtime and social contract

`BirdPondSchool` is one unsynced Udon component with 24 saved fish transforms.
It simulates at 10 Hz, interpolates positions and turns on rendered frames, and
does no catch-up burst after suspension. Speed and acceleration are bounded;
cohesion/alignment remain within each shoal, while separation includes other
shoals. Unequal slow wandering and shallow depth variation avoid a rigid ring
around a stationary target. Each shoal keeps its current target unless another
is meaningfully closer. Four groups need not service every simultaneous Bird.

Local points come from the acquired personal station. Remote points come from
the existing social presentation's accepted snapshot endpoint (`rawPosition`),
after its validity/expiry policy. Using that endpoint avoids false contact when
observer interpolation crosses the pond between two out-of-water samples.
Neither the distant render shell nor a renderer transform is an input. Remote
states remain unable to click, grab, teleport or authoritatively change objects.
The fish are a local ambient response, not network-owned gameplay entities.
Different clients may see different individual trajectories and phase; this is
deliberate and does not claim deterministic/shared fish positions.

The component discovers spawned player objects every two seconds and refreshes
after joins/departures. Discovery uses the supported
[Networking.GetPlayerObjects API](https://creators.vrchat.com/worlds/udon/persistence/player-object/),
not the disabled template. Cached storage admits the two local hands and up to
80 remote pairs. The simulation allocates no arrays per tick; discovery has
bounded-period player/object array allocations. No new synchronized variables,
network events or serialization streams are introduced.

World points are transformed into the pond's frame and rejected before steering
unless finite, within the water's bounding box, below its surface, above its
bed and within its sampled curved footprint. A 15 mm inset avoids rim ambiguity.
Schools follow the channel toward targets around the bend instead of cutting
through the dry inner terrace. A 55 cm shoreline margin contains the authored
fish bodies/tail animation. Simulation pauses beyond 100 m from the pond's
center; reentry keeps its bounded timestep. This is a shallow cosmetic water
volume, not a swimming/physics system.

## Editable assets

`Assets/BirdWorld/PondSchool` contains a conventional nested prefab, one original
88-triangle koi mesh, three pigment materials, shallow-water and basin-bed
materials. Fish have no colliders and are not static lightmap receivers. The
opaque bed reuses the existing water mesh 55 cm below the surface. The pond
surface uses a small transparent shader; fish use opaque vertex color and a
small vertex tail deformation. Both shaders support stereo instancing. The
sixteen earlier diamond fish remain inactive scene overrides for reference.
No third-party art was acquired.

The one-time **Bird > Coastal world > Add pond shoals once** command adds the
prefab and records the personal-station binding on its scene instance. It does
not regenerate the world or pond. Subsequent authoring uses the ordinary prefab,
materials and Inspector. If the pond profile changes, explicitly refresh the
school's sampled `centerline`, `waterHalfWidth` and `waterSurface` to match and
repeat containment/landing checks. A changed fish scale or tail shader requires
reviewing the shoreline margin. Rebuild normally through the VRChat SDK.

## Validation and limits

`Invoke-UnityCoastalWorld.ps1 -CheckFish` executes the compiled Udon VM in
ClientSim, including normal rendered frames. It checks above-water, dry-terrace,
below-bed, nonfinite and distant-point rejection; two local targets; stable
choice under neighboring-target jitter; curved-shore/end-cap containment;
speed limits; approach; remote snapshot input, interpolation exclusion, stale
packets/silence, departure and put-away; and disable/re-enable behavior. Remote
packets are explicit fixtures into actual SDK-created PlayerObjects, not a real
two-client test. A four-visitor/eight-hand fixture also measures 100 synchronous
compiled simulation steps. Its editor CPU timing is separate from actual Quest
frame samples; it is not a multiplayer or headset cost measurement. The existing
social regression runs independently.

The first visual review retained the recognizable fish and restrained palette,
but rejected tightly overlapping, nearly circular gatherings. Cross-shoal
separation and irregular wandering replace that candidate. The first motion
fixture also measured the jump between manually advanced and last-rendered
states; it now publishes that fixture state before measuring ordinary motion.
Read the newest checkpoint and curated Fish01 review for final results, actual
Quest evidence, timing measurements and the critic's remaining findings.
Unattended rendering does not establish physical Bird feel or multiplayer.
