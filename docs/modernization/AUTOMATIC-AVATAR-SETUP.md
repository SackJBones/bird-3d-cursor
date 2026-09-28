# Automatic avatar fingertips (Lab 13)

Bird starts from valid avatar bones without SET LEFT / SET RIGHT. ADAPTIVE
smoothing, PALM origin and Lab 12's sphere-center aiming policy are unchanged.
This removes a required setup action; it does not turn avatar bones into measured
physical fingertips. Lab 13 is now deployed and visibly running on the Quest;
physical estimation accuracy and feel remain for Dana to assess.

## Endpoint adapter

The SDK supplies three origins and a distal rotation for each finger, but the
adapter still needs a distal endpoint. Before learning a bone-local axis, it
continues the bend between the two observed phalanges by 0.7 times that bend,
on their unit-direction sphere. Estimated distal length remains 0.8 times the
preceding segment. Both are explicit Inspector assumptions. No universal
bone-local axis, Euler chart, torso or world-up direction is assumed. Collinear
segments use the last segment; an exactly reversed pair has no unique bend
plane and conservatively uses that same fallback.

Each finger independently learns its axis once its two observed segments are
nearly parallel (dot greater than .995). Non-thumb fingers must also point along
the palm rather than fold at the knuckle. Learning stores the current estimated
direction in the distal bone's local frame, so the learning frame's endpoint
does not jump. Later endpoints follow that bone's rotation, including distal
motion independent of the preceding joint origins. Axes then remain fixed until
the binding changes or the user returns to AUTO.

This is a heuristic: nearly straight preceding segments cannot prove that the
distal joint is also straight. Unusual avatar proportions, finger animation or
independent distal flexion at the learning moment can bias the estimate. Optional
REFINE retains the earlier straight-hand calibration path. A failed REFINE clears
the previous correction and resumes automatic estimates; it never blocks Bird
waiting for another button press. AUTO also clears the correction and learned
axes for that hand. Missing samples retain axes; avatar changes, disable and
relevant settings changes restart estimation.

`tipsReady` describes usable automatic or manually corrected endpoints;
`calibrated` specifically describes an explicit correction. Point presentation,
geometry diagnostics and the post-IK UI bridge consume readiness. Every axis
learning/reset boundary cancels filter and interaction history without dropping
the current usable sample, avoiding a synthetic reach gesture. Clicking remains
disabled pending separate avatar-click validation.

This logic belongs entirely in `BirdAvatarHandInput`. It introduces no new Bird
openness signal and does not change the fit, polynomial, radial continuation,
palm origin or sphere-center aiming policy.

## Validation and reproduction

`UnityAutomaticHandLabChecks` runs the saved scene's compiled Udon on ordinary
ClientSim frames. It checks automatic default-avatar startup, curled cold starts,
mirrored/rotated hands, .5x/1x/2x scales, exact fist return, independent per-finger
learning, MCP-folded rejection, independent distal motion, changed avatar bind
axes, all missing origins, disable/recovery, invalid settings, optional correction
and UI history cancellation, including automatic point-through interaction with either hand.
The complete lab suite passes 120,201 assertions over 997 frames on each editor
target; standard marker cadence/render checks
also pass.

The [endpoint measurements](measurements/automatic-setup.csv) report agreement
with known synthetic fixtures, not real fingertip accuracy. The initial fixture
uses the same 0.7 distal coupling assumption, deliberately exercising its
implementation under transforms and scale. Independent distal-motion and
changed-bind cases additionally exercise learned axes. Real avatar selection,
tracking loss and physical feel still need in-client testing.

Use `Invoke-UnityTrackingLab.ps1 -AddAutomaticSetup -Check -CheckBird` once to
update an older authored lab. Normal `-Platform Both -Check -CheckBird` checks
and builds the saved scene through the unmodified SDK; it does not launch a
client. On 2026-09-28 UTC, the existing validated Lab 13 Android artifact was
transferred to Quest, its device SHA256 matched, and the client was restarted
with the SDK's localWorldPath test-world intent. A fresh stereo capture shows
the Lab 13 title, automatic-start instructions and both hands' 16/16 avatar-bone
availability. No input source or runtime code was changed during deployment.
The source/generated comparison still matches all 82 runtime source/meta files.
See CHECKPOINT.md for exact build records and deployment state. Optional REFINE
is not needed to make Bird run; no hand-opening pose is required at startup.
