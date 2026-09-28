# Personal Bird in Bird World

Dana's next objective, 2026-09-28: visitors take their own Bird from the arrival
pedestal and immediately start pointing. Everyone can get one. There is no
limited shared pickup, ownership race, mandatory calibration or avatar swap.
The intended first embodiment is a dot with a trail, eventually changing color
on click. This work takes priority over further architectural polish.

## Current integration

The coastal scene uses an ordinary nested `Personal Bird station.prefab`, under
`06 Experience anchors`. The six architectural prefabs remain intact. The
station's touch light is above the existing arrival plinth. Touch it with either
palm for 0.12 seconds, or use VRChat's normal Interact action. Both hands receive
their own cursor. Collecting Bird disarms put-away until the player's body leaves
the 2.2 m vicinity. Return within 1.1 m and touch the light again (or use Interact)
to put it away. Vicinity uses the tracked head's horizontal position, including
physical room-scale walking with a fixed playspace origin. Merely withdrawing a hand or repeatedly clicking does not undo
acquisition. After putting it away, withdraw the hand and wait a second before
reacquisition. The label says to return here to put it away.

Acquisition and input are local per visitor and use `Networking.LocalPlayer`.
Another visitor cannot consume the light or take ownership of someone's cursor.
An optional [social presentation layer](SOCIAL-BIRD.md) now adds a per-player
snapshot stream and observer cursors/trails with contrasting player color pairs.
Its compiled-Udon/ClientSim verification is separate from the still-required
real two-client acceptance. Social Bird 02 now replaces Personal Bird 01 on the
Quest; its transfer hash and actual world rendering are verified. Physical
debounce feel and real multiplayer acceptance remain separate. There is no
account inventory grant or persistence across visits in this MVP.

`BirdAvatarHandInput`, `BirdSphereFit`, `BirdCursorState`,
`BirdSphereCenterFilter` and `BirdRangeAdaptiveFilter` are the same runtime
components as Lab 14. The authored scalar settings are compared against the
saved lab in validation. PALM origin, automatic estimated fingertips, front-palm
sphere-center aim, behind-palm correction, original range law and the current
filtering remain unchanged. Avatar bones are not raw tracked joints. Valid
finger bones are required; poor/non-humanoid rigs may not yield usable Bird.
The label asks for hands when input is unavailable.

`BirdPersonalStation` owns only the opt-in lifecycle. `BirdPointPresentation`
consumes the geometric point and owns the optional dot, far locator and trail;
it does not affect fit, filtering, range, or future interaction selection.
The presentation has a 32 mm near marble, plain inflation beyond 4 m and the
existing logical-depth shader. A 32-knot, 0.4-second ribbon stores logical points
and projects each knot separately, with width based on its own distance. This
keeps distant trails visible without moving the logical point or bringing a
far-sized ribbon into the working volume. Loss, disable, history reset,
suspension and player teleportation clear history. The shader retains world
occlusion, including when a logical point is beyond the camera's far plane.

The view supports a selected color, but estimated avatar clicks remain disabled:
availability of finger estimates is not validation of click quality. The current
milestone is pointing and moving a cursor. Do not silently enable clicks to make
this demonstration appear more complete.

## Validation and reproduction

Use `tests/Invoke-UnityCoastalWorld.ps1 -AddBird` only to add the station to a
coastal scene that does not yet have one. It refuses to replace an existing
station prefab. Subsequently edit the prefab normally, without regeneration.
Use `-CheckBird -Build -Platform Android` or `Windows` for the normal checks and
SDK exports. `-Check` additionally checks architecture, navigation and renders.

The compiled-Udon tests cover local touch/native acquisition, independent
prefab state, both hands, reentry, tracking loss, bounded trails and actual
cursor/trail rendering and occlusion from 1 m through 10 km. Dana explicitly set
10 km as the practical visibility requirement; 1000 km is optional research and
must not delay the MVP. An initial 1000 km probe with a 1 km camera far plane
returned zero colored pixels; its cause and relevance to the headset remain
unresolved. The accepted 10 km check uses the world's camera clip settings,
1024-square output and 4x MSAA, with enough elapsed time to produce a real trail.
Independent prefab instances are **not** a multi-client networking test.
See the dated checkpoint for actual results; running source tests does not
establish physical feel, headset performance or click fidelity.

## Next layers, in order

1. Dana accepted Personal Bird 01 and its working volume; verify the new pedestal
   debounce in a subsequent headset build.
2. Real two-client acceptance of social visibility, lifecycle, bandwidth and late joins.
3. Mid-range stability investigation and validated clicks/point-through UI,
   retaining deliberate acquisition rules and accepted visual inflation.
4. Versioned, reusable creator packages; portable embodiments as described in
   [BIRD-PORTABILITY.md](BIRD-PORTABILITY.md).

The lab remains available for diagnostics; its controls do not belong in the
arrival experience. Do not let Item SDK availability or avatar experiments
delay the usable world integration.
