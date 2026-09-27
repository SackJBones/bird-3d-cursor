# Connecting avatar Bird to local Udon interaction

`BirdAvatarUiInput` connects the experimental calibrated avatar source to the
existing logical UI. It does not fit a sphere, step a solver, estimate a click,
or read a displayed marker. The source remains an avatar-bone approximation;
availability and calibration do not establish physical tracking confidence.

## Authoring and timing

Assign one `BirdAvatarHandInput` and one dedicated `BirdUiPointer` to each
bridge. Leave the pointer's optional `cursor` poller empty and do not attach
another sample producer to that pointer. Both hands normally share the same
local `userId`. Configure the consuming router and spherical scroll with
`automatic=true` and `postLateUpdate=true`.

| Stage | PostLateUpdate order | Work |
| --- | ---: | --- |
| Avatar input | 0 | Read bones after IK and step the existing solver |
| Avatar UI bridge | 50 | Copy fresh accepted logical root, point and permitted press |
| UI router | 100 | Evaluate highlight/action state using that sample |
| Spherical scroll | 110 | Evaluate deliberate entry and integrate rotation |
| Lab status | 200 | Describe the interaction; text refreshes at 5 Hz |

The ordinary point/geometry views are a separate branch. Moving or disabling
their renderers does not change the UI point. In particular, the 500 m render
shell never limits logical interaction range.

The router and scroll retain their existing earlier `LateUpdate` default for
desktop and other ordinary producers. Their new phase switch is opt-in. Each
automatic consumer runs at most once per frame, including a phase change within
one frame. `automatic=false` preserves explicit `Process` dispatch. The bridge
also submits at most once per frame; disabling/re-enabling it in that frame
cannot create a second sample.

The bridge accepts only a current-frame source with valid calibration, data and
cursor tracking/pose. A missing or disabled source cancels its pointer. A
different input, cursor or pointer rebases history; the previous pointer is
released. Missed callback frames, a clock discontinuity or more than 250 ms
between accepted samples also rebase. Source calibration has an explicit
revision so RESET/SET or an avatar change cannot hide a discontinuity between
two consumer frames. Switching RAW/FILTERED rebases interaction history too.
These transitions preserve the downstream rule that recovery cannot synthesize
a press or reuse old spherical contact.

The bridge passes `cursor.clicksAllowed && cursor.selected`. The experimental
avatar source still sets `clicksAllowed=false`. This is a connection ready for
validated input, not a claim that avatar clicking now works. A conflicting
pointer cursor poller is rejected at the post-IK bridge stage; fix the authored
configuration instead of relying on that guard to arbitrate multiple producers.

## Optional station in Lab 06

The saved `BirdTrackingLab` adds **Reach station (right) / OFF** beside the low
front-row controls. Native VRChat Interact enables the station to the right of
spawn, facing the viewer and clear of the diagnostic boards. It begins off so the sphere-fit
inspection remains uncluttered. SET a hand at the existing console, enable the
station and move Bird inside its larger wire sphere. Extend through the back
to acquire it, then sweep/flick to rotate. Withdraw inside to coast. An outside
wrist sweep cannot start rotation, and losing contact requires a new interior
visit. Both hands use the same reusable component.

The twelve colored orbs use ordinary `BirdUiElement` hover feedback. They
highlight through the logical segment, but perform no color-selection action
because clicks remain disabled. Hover does not acquire spherical scrolling.
**RESET SPIN** uses native Interact to cancel motion and restore the authored
orientation. Turning the station off cancels UI input and inertia while leaving
the avatar calibration, solver, markers and geometry overlay available.

The station is local and unsynchronized. It adds no desktop or synthetic input
producer to the VR world. Its simple color/line artwork is a diagnostic, not the
recovered particle artwork or the final Bird World arrangement. Reusable input
and interaction live in the light repository; authored scene and Udon program
metadata live in the heavy repository. The lab-only reset/status component is
separate from the interaction components.

## Reproduction and evidence

Run the saved-scene checks and standard Android build without touching a device:

```powershell
./tests/Invoke-UnityTrackingLab.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/BirdWorld' -Check -CheckBird
```

`-AddUi` is an explicit one-time authoring step for a scene without the station;
it refuses to overwrite an existing station. Ordinary checks/builds reopen the
saved scene. `-Launch` separately requests the standard SDK BuildAndTest path;
it is not part of the command above.
Use `-SkipBuild` with checks while iterating on tests; it leaves the previous
bundle intact and cannot be combined with `-Launch`.

The avatar test's UI extension runs through actual compiled Udon and normal SDK
post-IK dispatch. Its articulated bone fixtures vary bend and rigid hand pose;
the production adapter and solver still calculate the point. It checks exact
same-frame root/point copying, one sample per frame, highlight, deliberate
entry, wrist drive, withdrawal/coast, native reset, both hands, lost/stale data,
rebinding, calibration and filter transitions. It also checks the existing
rendering and avatar contracts. These fixtures do not measure physical hand
accuracy, headset frame pacing or subjective feel. See CHECKPOINT.md for final
results, artifact hashes and the separately deployed revision.
