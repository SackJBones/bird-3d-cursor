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
their own cursor. Interact again at the pedestal puts Bird away. After putting
it away, withdraw the hand before touching again to avoid immediate reacquisition.

This is local per visitor: all station/input/presentation programs are unsynced,
and use `Networking.LocalPlayer`. Another visitor cannot consume the light or
take ownership of someone's cursor. **Other people do not yet see your cursors.**
There is no account inventory grant or persistence across visits in this MVP.
Synchronized social presentation is the next integration layer, preferably a
per-player object rather than a scarce shared pool.

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

1. Headset acceptance of the pedestal and the inherited Lab 14 hand behavior.
2. Social visibility with per-player lifecycle, bandwidth limits and late joins.
3. Validated clicks and point-through UI, retaining deliberate acquisition rules.
4. Versioned, reusable creator packages; portable embodiments as described in
   [BIRD-PORTABILITY.md](BIRD-PORTABILITY.md).

The lab remains available for diagnostics; its controls do not belong in the
arrival experience. Do not let Item SDK availability or avatar experiments
delay the usable world integration.
