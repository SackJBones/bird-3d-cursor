# Palm direction and the reference ray origin

## Current direction contract: Lab 12

Dana physically confirms Lab 11's 45-degree **knuckle-axis** fallback is the
right vector, but rejects its use as the general far direction: it removes
Bird's important ability to aim by reshaping fingers without turning the wrist.
The current contract supersedes the earlier range-gated direction policy:

- A valid sphere center on or in front of the plane through the selected palm
  origin with the tracked palm normal determines the raw ray at **every range**.
  Neither flat-hand blend weight nor fit confidence changes that direction.
- A center behind that plane blends toward the accepted 45-degree knuckleward
  fallback. The blend depends only on signed center depth divided by center
  distance, zero at the plane and full at a .25 ratio behind it. This exposed
  geometric width is not a hand-openness signal. Intrinsic unit-sphere blending
  avoids zero-vector cancellation; any exact-antipode path uses hand axes.
- A singular fit or center exactly at the origin has no defined fitted direction
  and uses the fallback. Full fist return remains exactly at the selected origin.
- Direction is separate from radial range. The existing untilted scalar range
  continuation and original polynomial are retained. ADAPTIVE still smooths the
  resulting point; the sphere-center collinearity contract describes raw geometry.

`BirdCursorState.useSphereDirection` enables this policy in the lab; older
callers retain their existing behavior when it is false. `insideOutWeight`
reports actual correction separately from the radial `limitWeight`.
`-RefineSphereDirection` authors Lab 12. The earlier `-RefineFarTilt` authoring
switch and range-gated direction implementation are retired, while their measured
CSV remains a historical comparison.

Dana explicitly asks for ADAPTIVE and PALM as defaults and removal of both
comparison buttons. The lab now saves those per-hand settings, removes both
button meshes/colliders/labels, and retains nonvisual policy components for
authoring and regression checks. No point/origin mode action is required on entry.
Required SET removal remains the next separate setup task. See the latest
CHECKPOINT for verified deployment, tests and subsequent physical feedback.

Lab 12 passes 119338 compiled-Udon assertions over 868 normal frames on both
Android and Windows, including saved ADAPTIVE/PALM defaults and absence of both
button meshes/colliders/labels. The fixed-palm finger-splay sequence moves the
distant ray by more than three degrees on each hand while every valid front-side
sample remains aligned with the fitted center. The dedicated 1754-row replay
covers front, behind and singular fits in four palm orientations and both hands,
continuous palm-plane crossings, exact fist return, and radial preservation
(maximum relative range difference 1.097e-6 from float rounding).
[udon-center-direction.csv](measurements/udon-center-direction.csv) contains these
synthetic comparisons. The original 114 cursor/filter and 5549 hand-limit
regressions also pass with the optional policy off.

The Lab 12 Android artifact is hash-verified in the Quest VRChat TestWorlds
directory. Its first SDK launch occurred while the headset was asleep. On
2026-09-28 UTC, Dana requested this exact revision while wearing the headset;
the existing 270827-byte bundle was rechecked against SHA256
`56DF58AE27ED2DC95219F4CF7C40349AEE1B5F6CEC58A3EBCABC1442CAC46FD5`
and relaunched with the SDK's Android localWorldPath intent. A fresh stereo
capture confirms the Lab 12 title and live hand markers. Physical feel remains
for Dana to assess. Lab 13 automatic setup is built but was not substituted for
the requested Lab 12, which still requires SET LEFT / SET RIGHT once.

## Earlier comparisons and evidence

Dana reports palm-up opening sending Bird sideways in RAW on both hands in
Lab 08. This observation places the problem before the optional filters.
The root hypothesis remains worth inspecting, but the source audit finds the
same reference formula: 60% index base plus 40% thumb base. The avatar adapter
uses index/thumb proximal bone origins for those roles; matching the formula
does not prove those avatar landmarks match the original physical skeleton.

Lab 09 isolates two direction changes while preserving that root, fit points,
sphere solver, polynomial, hand-size normalization and filter choices:

- The avatar palm normal now comes from wrist-to-mean-knuckle and
  index-to-little-knuckle vectors. The old thumb/index/little plane could tilt
  when the opposable thumb moved, even if the palm itself did not turn.
  Handedness fixes the sign; no head, torso or world-up reference is used.
- The authored lab's flat-hand tilt is zero, so its far fallback follows that
  normal. The earlier 45-degree knuckleward preference remains an available
  parameter, but is not applied in this diagnostic comparison. Standalone
  source settings and its installed app are unchanged.

The palm normal also feeds the existing flat/fist calculation, so correcting it
can change range as well as direction even though the range parameters are
unchanged. For example, the synthetic ClientSim default open hand now reaches
the full flat endpoint. This is not claimed to be a purely visual change.

The geometry overlay adds a small white cross at the exact logical root. Gold
still marks the actual fit and root-to-center ray; green shows the palm normal,
and cyan/pink shows the resulting Bird. The existing limit blend can still
make the final direction differ from the gold ray. The overlay does not move
the root or fabricate a fitted sphere to explain that difference.

Dana's clarification is authoritative: nearby motion should behave like classic
Bird. A tilt may return later only as a smooth **far-field** preference, using
sphere geometry to weight it. Avoid surprise during opening with a stationary
wrist. Do not introduce another hand-openness quantity or reinstate the tilt
until the simpler comparison is assessed. Existing flat/fist limits still have
older bend-based heuristics; this direction correction does not claim to have
replaced that range policy with a sphere-only continuation.

The dedicated compiled-Udon checks compare the same fixed-wrist opening under
45/0-degree settings, verify the reference root and visible cross, thumb-motion
independence of the palm frame, both hands, palm-up/down and rotated poses,
and rejection/recovery for a degenerate wrist/knuckle plane. These synthetic
checks isolate the mechanisms; physical avatar landmarks and perceived direction
still require feedback. Read the latest CHECKPOINT for measured results and
whether this revision has been loaded on the headset.

Use the one-time/update authoring switch `-RefinePalmDirection` with the normal
tracking-lab runner to set zero tilt and add missing root crosses. It preserves
existing geometry and can run after older layout-refinement switches. Normal
reproduction uses the committed authored scene without this switch.

## Measured synthetic comparison

Both Android and Windows compiled Udon pass 110238 assertions over 743 normal frames for Lab 09, including
the existing avatar/filter/UI/symmetry regressions. With the same synthetic palm
held upward, 50 degrees of fixture finger bend places the ray 6.25 degrees from
the palm normal under either tilt setting. At 30 degrees bend, the old tilt is
17.84 degrees off-normal while zero tilt is 2.72 degrees off-normal; by 15 degrees
bend, they are respectively 45 and zero degrees. Maximum angular change per
half-degree opening sample is 1.187 versus .135 degrees. These fixture bend
labels are test inputs, not a new runtime openness measure.

A separate thumb-only displacement sequence rotates the old computed normal
by up to 65.69 degrees while the wrist and knuckles stay fixed. The new normal
is invariant to that sequence within the test's float tolerance. The root still
follows the original weighted formula, so moving its thumb landmark legitimately
moves the root; the new white cross exposes this instead of hiding it.
Measurements are retained in [udon-palm-direction.csv](measurements/udon-palm-direction.csv).

## Device comparison

Lab 09 is now loaded in Quest VRChat through the ordinary SDK BuildAndTest path.
The device hash matches the validated Android artifact and a stereo capture
shows the Lab 09 room and both hand diagnostics. The capture begins awaiting
SET calibration, so this establishes scene execution rather than acceptance
of the corrected direction. Dana has been asked to compare palm-up opening
and inspect the white root cross. No further headset reload is needed for
parallel editor/platform validation.

## Lab 10: include the pinky in the ray origin

Dana physically sees the Lab 09 white cross on the line between index knuckle
and thumb base and asks to include the pinky knuckle to bring it toward the
palm center. The reference formula is therefore no longer the preferred sole
origin. The next authored comparison defaults to
`.3*indexBase + .3*littleBase + .4*thumbBase` and supplies an **Origin / PALM**
native console control that switches to **CLASSIC** (`.6*indexBase+.4*thumbBase`).
This is an initial palm-centered choice, not a universal anatomical center.
`BirdAvatarHandInput.littleFingerRootShare` exposes the pinky's share of the
60% knuckle term; zero preserves the reference and .5 divides it equally.

The origin affects both ray direction and sphere-center distance supplied to
the polynomial; those are intended geometric consequences. It does not alter
the fitted sphere, sampled joints or palm normal. The existing limit code used
the classic root to reconstruct a thumb base. `BirdCursorState` now accepts an
explicit thumb base, supplied by the avatar adapter, so choosing a ray origin
cannot silently rotate that palm frame or change the bend-based limit weights.
Legacy callers retain the original recovery path when the option is off.

Origin selection preserves fingertip SET calibration but clears temporal and
UI contact history, including a round trip within one observed frame. It must
not synthesize a sweep through a nearby selector. The white cross follows the
actual selected origin. The far tilt remains zero for this comparison; any
future tilt should be limited to the far field and leave classic nearby geometry
intact. Tests and headset status for this extension belong in the latest
CHECKPOINT; Lab 09's verified deployment above is a separate checkpoint.

The Lab 10 Android and Windows compiled-Udon suites pass 110449 assertions
over 779 normal frames each. Across both mirrored hands, six opening/closing poses and two rigid
orientations, the synthetic origin moves inward by 16.475 mm; the fitted center
and existing bend value are unchanged. Ordinary unblended rays continue through
the fitted center, full fists return to the selected origin, invalid weights
pause safely, and source/control round trips clear UI contact history.
[udon-palm-origin.csv](measurements/udon-palm-origin.csv) retains these synthetic
measurements. This demonstrates the intended separation, not a measured universal
palm center or physical acceptance.

The Lab 10 Android bundle is now hash-verified on Quest and actually rendering
inside VRChat. The device capture shows Origin / PALM, the fitted sphere, white
origin cross and live Bird, with the user already in FILTERED mode. RAW and
SPHERE remain available. This supersedes Lab 09 as the active headset scene;
Dana has now physically compared the origins: PALM feels "much better" and the
geometry "way more familiar". ADAPTIVE also seems good; SPHERE was barely tried
and is not accepted on that basis. The original core
regression also passes 114 cursor/filter assertions plus 5549 hand-limit
assertions over 2401 synthetic articulated samples with the legacy thumb-base
recovery path, so the standalone caller contract remains intact.

## Lab 11: far-only rotation about the knuckle axis

Dana requests the far continuation turn 45 degrees toward the fingers **about
the knuckle line**, not point toward the center of the knuckles. Preserve the
now-liked palm origin and nearby geometry. `useFarFieldTilt` is an optional
cursor policy; older callers keep their earlier behavior when it is false.
The lab enables it with `flatDirectionDegrees=45`. The authoring entry point is
`-RefineFarTilt`; normal builds use the saved scene.

That superseded policy first computes Lab 10's untilted vector and its original
polynomial reach. Through four metres the vector is unchanged exactly. A
smoothstep between four and 25 metres multiplies the existing limit weight
and the 45-degree correction. Thus an ordinary unblended sphere fit is also
unchanged at any distance. Four and 25 metres are exposed provisional defaults,
not newly inferred hand-opening thresholds. The gate uses the incoming point,
never the filtered point. It introduces no new openness quantity; the older
bend-based limits are still present and should not be described as sphere-only.

Rodrigues rotation about the index-to-pinky axis changes the direction of the
free vector, preserving its length and polynomial range up to float rounding.
The axis sign is resolved by the hand's palm frame, so the normal turns toward
the fingers on either hand, including upside-down poses. There is no torso,
head, world-up or Euler-angle state. At the full far limit the resulting
direction is exactly 45 degrees from the normal. Intermediate healthy fit
directions are retained in proportion to their existing geometric weight.

The compiled-Udon checks replay both hands in four palm orientations, compare
against an independent quaternion rotation about the actual knuckle span,
check exact unchanged nearby points and healthy fits, retain exact fist return,
and sweep the range gate at 10 cm intervals. Deployment and measured results
are recorded in the latest CHECKPOINT; an authored policy alone is not a claim
that Dana has tested it.

Both Android and Windows now pass 118371 compiled-Udon assertions over 1199
normal frames, plus the saved-scene cadence/render checks. The dedicated tilt
replay records 1330 comparisons, with maximum relative range difference
1.554e-6 from float rounding and zero reported angular difference from the
independent quaternion oracle. Gate samples at 10 and 15 metres turn 8.921 and
24.106 degrees at full limit weight; 25 metres reaches 45 degrees. Near points
and unblended healthy-fit points match the untilted result exactly. The original
114 cursor/filter and 5549 hand-limit regression assertions also still pass.
See [udon-far-tilt.csv](measurements/udon-far-tilt.csv) for synthetic measurements.

The Lab 11 Android artifact is hash-verified on the Quest and a stereo capture
shows it running inside VRChat with the palm cross, fitted sphere and live hand
diagnostics. Dana is invited to assess its far direction; this is separate from
the earlier positive Lab 10 origin/ADAPTIVE feedback. The build still requires
manual SET; automatic setup below is queued next.

## Next interaction setup priority

Dana requests eliminating required SET LEFT / SET RIGHT in the near future;
the repeated setup is disruptive. Estimate the missing avatar fingertips
automatically where possible, retaining explicit calibration only as an optional
correction. Keep this in the avatar adapter, not the geometric solver. Do not
assume all avatar distal bones share a Unity axis, silently calibrate a fist as
an open hand, or add a new openness signal to Bird's range law. Test startup in
curled and open poses, mirrored/custom bone axes, avatar changes, scale, loss and
recovery, and preserve cancellation of UI/filter history if the estimate changes.
This request follows the Lab 10 feel confirmation; avoid changing its accepted
palm origin or range law while removing the setup barrier.
