# Finger tap practice

The coastal world has a secondary practice display beside the arrival area.
Get Bird normally, approach the display, and lower/raise each index finger.
Each hand has its own meter, a white press-depth line, a state light and a tap
count. The colors follow that visitor's contrasting hand pair. Words accompany
the colors: SHOW HAND, RELEASE TO BEGIN, RELEASED and PRESS.

This is read-only feedback. It cannot enable clicks, submit UI input, alter the
Bird point or teleport the player. The saved avatar click gate and travel gate
remain off. Physical avatar fingertip/click fidelity still needs testing.

## Component contract

`BirdClickPractice` is a separate unsynced Udon component with ordinary Inspector
references to the station, two avatar sources, their optional presentations,
and the display's Text/Image elements. It reads after avatar IK. Only a fresh,
valid current-frame input sample with usable click geometry is displayed.
`BirdCursorState.clickAvailable` exposes the existing internal click-usability
decision independently of action permission; the point, click law, filter and
range calculations are unchanged. Invalid geometry/cancellation clears it.

The preview uses the existing strict 7 mm press / 5 mm release hysteresis.
It requires a release first on entry, recovery, history/source changes or a
missed sample, so arriving with an already bent finger is not counted as a tap.
The meter shows zero to 14 mm of inward depth, with the press line halfway;
the numeric reading also exposes negative/out-of-scale values. Text updates
at 10 Hz, while meters/lights consume each fresh frame. The display is active
only within its configurable nearby practice radius and with Bird acquired.
Leaving or putting Bird away ends and clears the visit; tracking loss clears
contact while preserving the count for that visit.

The prefab has internal artwork references; only scene-specific Bird bindings
are overrides. Move the whole display, change its artwork, or tune its nearby
activation radius without touching the hand solver. It introduces no network
stream and records no hand motion to disk.

## Physical follow-up

When a person is available, check both hands with a relaxed curled hand, a mostly
closed hand with only the index extended, and several longer Bird ranges. A
deliberate index depression should show one PRESS and increment once; holding
should not repeat, and releasing should clearly reset it. Observe noisy resting
poses, tracking loss/recovery and different avatar sizes. A stable counter alone
does not prove the inferred endpoint matches the real finger.

This display prepares that check while avoiding action side effects. Synthetic
tests compare fresh production adapter outputs with the display and explicitly
exercise threshold boundaries, hold/release, invalid depth, history gaps,
put-away, distance gating and read-only behavior. See the latest CHECKPOINT for
completed platform/device results. Unavailable physical hands must not stall
independent world work.

## Independent review, Beacons02

The critic accepts the bounded diagnostic panel. Captured at 2.7 m, its title,
instructions, states and meters are readable. Left/right words and colored bars
retain hand identity while press lights become white. Its side placement keeps
it out of the central pickup composition; first-visit discovery is not proven.
Practice-only scores: aesthetics 6.5, readability 7.5, secondary placement /
hangout fit 7, overall diagnostic presentation 7. Full-world scores are unchanged.

The board is useful test furniture, not final architectural presentation. Small
follow-ups: use PRESSED rather than PRESS, singular 1 tap, and show the release
threshold as well as the press threshold. A held press between 5 and 7 mm is
correct hysteresis but the single white marker does not explain it. The small
footer is the weakest text. These are presentation follow-ups, not changes to
the underlying click law or grounds to enable unvalidated actions.
