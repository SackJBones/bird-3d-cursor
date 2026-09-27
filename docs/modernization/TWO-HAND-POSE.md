# Optional two-hand pose clutch

`BirdTwoHandPose` is an ordinary Unity input adapter for the existing bounded
grab transaction. It supplies orientation and uniform-size requests; the Bird
solver still supplies only its geometric point. The adapter neither changes
fit/range/filter/click laws nor writes the held Transform. Hanoi can keep its
translation-only policy, and another experience can choose different gestures.

## Interaction and authoring

Assign one `BirdGrabInteractor` and the two `BirdPointerInput` components. Their
`Origin` values must be tracked hand roots in world meters, not a shared head or
camera ray origin. Both inputs must represent the same user. The held target
must permit rotation and/or scaling using the authoring contract in
[OBJECT-POSE.md](OBJECT-POSE.md). No hand SDK, custom Inspector or new dependency
is required. Engaged and Disengaged are ordinary serialized UnityEvents.

1. Point through an object and hold its normal Bird press with either hand.
2. Point the other Bird through that same held box and make a fresh press.
   An already-held or simultaneous second press does not join automatically.
3. While both hold, turn the line between the two hand roots to turn the object;
   change their separation to resize it. The primary Bird still controls
   translation through the existing workspace and approach guidance.
4. Release the second press to freeze rotation/size at their displayed values,
   immediately stopping any remaining pose-follow lag. Keep translating with
   the first hand, or re-clutch from this displayed pose without a jump.
5. Release the primary press to attempt normal placement. The dock still checks
   both requested and displayed pose, final bounds and the experience's rules.

The initial hand span and current displayed pose are captured per clutch.
Rotation uses the shortest rotation between initial and current span directions;
size is the captured factor multiplied by the span ratio. Axes forbidden by the
target remain fixed. Authored parent rotations are composed independently of
mirrored/nonuniform scale, as in the existing pose command adapter. Bird range
does not multiply rotation or size sensitivity, even for a million-meter point.

Two points do not determine twist about the span axis. This adapter deliberately
does not infer that missing degree of freedom from the torso, head or wrist
orientation. Default valid span is 0.06–3 m, and a clutch stops beyond 160 degrees
from its captured direction, before the antipodal ambiguity. Release and re-grip
to continue turning. These are configurable gesture limits, not cursor limits.

## Ownership, loss and timing

The optional adapter runs in LateUpdate at order 110, before the grip's order 120.
Both inputs must have published new revisions before a continuous gesture update
is applied. A final available sample can be included on primary release. There
are no timestamps in this input contract; revision pairing is not a claim of
perfectly synchronized physical measurements. A stream older than 0.25 seconds,
an invalid time step or lost tracking stops the clutch. Origins and points are
never rewritten. Hosts can disable automatic processing and call `Process(dt)`.

`BirdGrabInteractor.StopHeldPose(blocked)` freezes at the displayed pose and
preserves any existing PoseLimited state. Lost/stale/invalid secondary input
uses `blocked=true`: placement stays blocked until fresh valid pose intent.
This prevents a lagging, apparently aligned display from becoming an unintended
drop when a contrary gesture loses tracking. A fresh valid re-clutch can recover.
An explicit secondary release or adapter Cancel/disable freezes deliberately
without clearing an earlier limit. Primary tracking loss rolls back the whole
transaction under the existing grip policy.

Out-of-range size requests are rejected but leave the clutch active so the user
can adjust back. Degenerate or antipodal spans end it and require a new press.
Changed ownership, target/parent/workspace, missing components and lifecycle
changes cannot keep an orphan clutch alive. A competing explicit pose command
disengages this adapter without replacing that command. Configure references
between transactions, and use one input/pose owner at a time.

## Standalone Quest harness

Quest v0.10 adds an **Objects** fingertip label beside Record 20s. Hold a fingertip
there for 0.6 seconds to exchange Colors for Objects; move away before toggling
again. Colors remains the initial mode. Switching modes cancels/restores an
unfinished object move and consumes existing input revisions, so a held click
cannot transfer to a different experience. The larger color sphere retains its
back-surface-only scrolling gate.

Objects uses the same accepted hand-root/logical-point/press streams already
feeding Colors. A tabletop station appears ahead-left, and the 69 m building
station sits 650 m into the valley. Each second dock requires a 90-degree turn
and 1.25x size. Green means release can place. Closing the primary hand keeps the
entire held building in its distant workspace. The view anchors the authored
environment once; it does not supply gesture directions or change Bird geometry.

This is the standalone scale harness, not the intended final Bird World.
Deployment details belong in [QUEST-LIVE-HANDS.md](QUEST-LIVE-HANDS.md).

## Evidence, 2026-09-27 UTC

`Invoke-UnityPoseChecks.ps1 -BuildPlayer` now runs the separate two-hand fixture
alongside the existing 6015 pose assertions. The new fixture passes **4863**
assertions in real Unity 2022.3.22f1, mostly individual box-corner checks along
trajectories. Cases include both primary roles, no-jump engagement/reentry,
rotation-only/size-only permissions, freeze, invalid spans, rejected intent,
other-user exclusion, pairing/staleness, loss/destruction/disable, callback
cancellation, competing commands and distant unchanged logical points. The
Windows player uses normal Update/LateUpdate for its two-hand tabletop docking,
then checks the distant boundary, loss return, rendering and cleanup.

[Synthetic rate measurements](measurements/two-hand-pose-rates.csv) give
89.3935852 degrees and factor 1.49590731 after 0.5 s at rate 10 for a constant
90-degree/1.5x request, identically at printed precision for 30/72/120 Hz. This
does not establish equivalence for differently sampled changing gestures.

`Invoke-UnityQuestHandsBuild.ps1 -CheckObjects` runs the real host factory,
placement, accepted-input binding and normal frame order before building Android.
It checks tabletop pose docking, a rotated/resized distant building remaining
over 450 m away on closure, mode-switch full rollback, fresh-press requirements
and restored color selection inside the surrounding sphere. Four real vista
captures are inspected. Per-frame assertion totals vary with editor frame rate.
The inputs are synthetic accepted states, not physical XR measurements.

Physical two-hand comfort/precision, mixed device cadences and performance remain
unassessed. The live VRChat hand path still needs click-fidelity validation. Collision
sweeps/lift, dynamic occupancy, multiplayer reservations and real VRChat client
testing remain separate work; per-frame workspace containment is not collision
avoidance or network authority.

## Local Udon adapter

`Integrations/VRChat/BirdObjectTwoHandPose.cs` ports the clutch into a separate
UdonSharp component, with no networking or changes to Bird point/range/filter
math. Bind one `BirdObjectGrip` and two dedicated `BirdUiPointer` producers.
Both pointers must belong to that grip, have the same user identity, and supply
distinct hand-root origins in world metres. A shared head origin cannot supply
the span. Targets must opt into rotation and/or scale as before.

The adapter runs at order 110, after the UI router (100), before the grip (120).
Both default to ordinary LateUpdate. For an after-IK producer such as
`BirdAvatarUiInput`, set `postLateUpdate` on the router, gesture and grip together.
Automatic dispatch is at most once per frame; explicit `Process` accepts
`stepDelta` for a host-owned loop. Changing phases or bindings consumes existing
presses. An automatic phase mismatch cannot acquire. Keep a single producer per pointer and
a single active gesture controller per grip.

The grip exposes `AllowsInput` so the gesture shares the existing viewing-area,
menu-focus and per-sample `uiConsumed` exclusions. A UI-owned secondary cannot
join or continue a clutch. Primary loss/menu exclusion still rolls back the
whole transaction. Secondary loss freezes pose, marks the request limited, and
requires fresh valid intent before a drop. `StopHeldPose(bool blocked)` freezes
the displayed rotation/scale without clearing a previous rejected request.
Disabling just the gesture relinquishes rotation/scale while retaining primary
translation; an experience switch should also disable/cancel the grip and cancel
its pointers. Re-enabling cannot replay an already-held secondary press.

Optional engaged/disengaged callbacks use the conventional Udon event-target
and event-name fields, separate from target grab/place/cancel callbacks.
The authored `BirdPoseDemo` binds the new component to its existing two pointers
and shared grip. Its desktop mouse producer still drives only the first pointer;
Q/E and scale commands remain usable and take over from an active clutch.
This binding does not invent a second hand or enable avatar clicks. Tracking Lab
05 remains deployed for physical geometry inspection; neither the lab nor the
installed standalone app is replaced by this work.

Run `tests/Invoke-UnityUdonTwoHandChecks.ps1 -UnityEditor <Unity.exe>
-ProjectPath <BirdWorld> -Platform Both -Regressions -BuildWorld` to restore sources and run the
saved scene through actual compiled Udon. `-Author` explicitly adds the component
only when absent and refuses to overwrite an authored binding. The normal-frame
sequence covers tabletop LateUpdate and distant-building PostLateUpdate input;
contracts cover release/re-clutch, stale/mismatched samples, callbacks, permissions,
ownership and exact-pose placement. Rendering happens after completed transactions
so synchronous GPU readback cannot masquerade as a live input pause. Logs/results
and images remain in the ignored project/Validation directories. See the latest
checkpoint for measured results and SDK build evidence.

The runner defaults to Windows; `-Platform Android` or `Both` selects mobile or
both editor targets. Each selected target runs the gesture suite, optional
regressions, and optional normal SDK build and independent bundle-catalog read.
Platform-suffixed logs/results are retained. These builds never launch a client,
contact the headset or upload. Catalog loading is not client execution.

The compiled-Udon constant-request [rate measurements](measurements/udon-two-hand-pose-rates.csv)
give 89.3935852 degrees and factor 1.49590743 at all three printed cadences.
The factor is within 0.00000012 of the ordinary fixture, which uses different
world-space root coordinates. This is not a claim of bit-identical arithmetic
across the two fixtures. Differently
sampled moving hands and headset pacing still require separate assessment.
