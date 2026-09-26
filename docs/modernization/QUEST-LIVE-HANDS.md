# Quest live hand comparison

This standalone `Bird Live Hands` app feeds real Quest finger joints into the
existing Unity `Bird.cs` core and into the port's `BirdSphereFit` and
`BirdCursorState` sources. Both receive the **same original 16 fit points**, root
and index fingertip. There is no avatar calibration, hand-size normalization or
range multiplier. This is a physical math comparison, not a VRChat world.

Version 0.1 was physically tried by Dana, who reports an extremely small port
gap. Version 0.2 adds experimental palm-side continuation and optional depth
presentation. Dana later reported abrupt capping and a closed-fist maximum-range bug; that continuation is replaced in v0.5. See
[geometry and presentation](GEOMETRY-AND-PRESENTATION.md) for the separation,
parameters, test evidence and remaining limits.

Version 0.3 fixes the trail tip detaching between history samples at higher
update rates. Rapid-return and mixed-depth mesh tests pass at simulated
30/72/120 Hz; they do not establish headset frame pacing or subjective feel.

Version 0.4 integrates outward size growth between samples to reduce the
measured update-rate bias, and stops catch-up growth when the target starts
decreasing. Immediate near-size restoration remains in place. See
[the growth experiment](DEPTH-GROWTH.md) for the numerical comparison.

Version 0.5 preserves ordinary fits and replaces the cap with separate
pose-aware flat/fist limits. The sphere diagnostic fades as its contribution
fades; the colored point is the limited/filter result. The new **Record 20s**
fingertip control has a three-second countdown and saves both hands locally.
See [hand limits and recording instructions](HAND-LIMITS.md). Natural feel and
the recording control still need a physical attempt.

Version 0.6 tilts fully open reach 45 degrees toward the knuckles, entirely
within the hand frame. Plain Inflate is the new default. A simple open-front
house, furniture, terrace, lake and full-size distant buildings provide scale
references. Dana's v0.5 recording has been retrieved and replayed locally; see
[the vista and replay notes](QUEST-VISTA.md). Dana subsequently reports that
the range transition feels very good, but far visibility and closed-hand
clicking need improvement.

Version 0.7 preserves that range/aim/filter behavior, strengthens far cursor
and trail presentation, adds a contrasting locator edge, and restores
[closed-hand index-lever clicks](CLOSED-HAND-CLICKS.md). The house now sits on
a high overlook, with the valley roughly 150 m below, nearby shoulders and
distant mountain layers. Height labels are removed. Near cursor/trail sizes
remain 32 mm/2 mm through 4 m. The separate return-filter experiments are not
part of this app version.

The runner copies the two production port sources into an ignored generated
project with `QuestHandsUdonShim.cs`. They execute as ordinary C# MonoBehaviours.
This does **not** exercise the Udon VM, the 12-point avatar-bone approximation,
VRChat tracking, networking or publishing. Separate ClientSim checks cover the
compiled Udon math. A physical VRChat-world comparison remains a separate step.

## Build and launch

Use Unity 2022.3.22f1 with its Android SDK, NDK and OpenJDK modules:

```powershell
./tests/Invoke-UnityQuestHandsBuild.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/Validation/QuestHands2022'
```

The directory must be new or carry the exact dedicated generator marker.
Pinned providers are OpenXR 1.10.0 and XR Hands 1.3.0. The bootstrap enables
`BIRD_OPENXR_ENABLED` and the new Input System before the build invocation.
Android uses OpenXR, Meta Quest Support (including Quest 3), Hand Tracking
Subsystem and the optional Oculus Touch interaction profile. XR Hands writes
the hand-tracking manifest entries; controllers are not needed by this scene.
The APK uses ARM64 IL2CPP, Linear color, GLES3, minimum API 29 and target 32.

`hands-math-result.txt` records preflight parity checks. `hands-build-result.txt`
and `hands-build.log` record build success separately from device deployment.
APK: `Build/BirdLiveHands.apk`; package: `org.bird3d.livehands`.

```powershell
adb install -r '<generated-project>/Build/BirdLiveHands.apk'
adb shell am start -n org.bird3d.livehands/com.unity3d.player.UnityPlayerActivity
adb shell pidof org.bird3d.livehands
adb logcat -d -s Unity
```

Inspect `BIRD_HANDS_START`, `BIRD_HANDS_SUBSYSTEM` and `BIRD_HANDS_STATUS`.
An installed APK, running app, active XR display, running hand subsystem and
valid tracked hands are distinct checks. Keep device serials and personal
device logs out of public commits.

## In-headset check

Hold a hand comfortably in view and cup it as if holding a small ball. Cyan is
left, pink is right. The colored wire sphere shows the actual fit and fades when a limit law takes over;
small joint markers show the skeleton used by Bird. Green marks the weighted
hand root and a green line shows palm front. The index fingertip is white.

- **Colored cursor and short trail:** pose-limited port point after the standard
  Kalman recurrence. It brightens while selected without changing near size.
- **Small white cursor:** blended geometry through the original range polynomial,
  before filtering.
- **Gold ring:** unchanged legacy core's filtered point. Ordinary curved poses
  should closely overlap; near flat/inverted fits intentionally diverge.

Hold an index fingertip on a mode label for 0.6 seconds to compare Fixed,
Inflate and Inflate + lag. All retain 32 mm physical cursor diameter through
4 m. No controllers are needed. These are diagnostic presentation alternatives.

First cup/open slowly to assess sphere center, radius and range response. Then
hold still to compare raw jitter with the colored cursor; draw slow loops and
make short movements/stops to assess filter lag. Bend the index into/out of the
sphere to check selection. Finally move a hand out of view and back to inspect
loss/recovery. Repeat with the other hand and with both hands together.

The display reports fit radius, effective range-input distance `d`, bend and blend weights, raw range, original
and port click states, and center/raw/filtered differences in millimeters.
Original mapping is `d + d*d/.02 + .02*(d/.03)^6`; Kalman is `Q=.001`,
`R=270*d^3`. Open, nearly planar hands can produce very long ranges. The demo
no longer hides the cursor beyond 20 m. A display-only depth shell preserves
direction/angle while keeping astronomical points inside the camera clip.
Logical interaction coordinates retain their full range. The flat-hand law
ends at a 2 m range input, mapping to roughly 1.76 billion meters of raw reach.
See the architecture note for numerical and far-world occlusion limitations.

## Known differences and boundaries

- The original core solves the centered 4x4 normal equations. The port solves
  the equivalent centered, scale-normalized 3x3 system and rejects normalized
  determinants <=1e-6 in its default mode. This comparison opts into the new
  pose-aware point law near flat/fist limits; the legacy core is unchanged.
- Original Kalman state starts at zero and is retained over tracking loss.
  The port seeds at the first valid measurement after startup/loss. This can
  produce visible transient differences even with identical coefficients.
- The demo captures the package's actual `OpenXRHand` adapter into a snapshot
  once per XR Hands **Dynamic** update. It does not update the filter again in
  BeforeRender. Camera poses and all joint/cursor geometry share unscaled
  tracking coordinates. Head pose also updates before rendering.
- The original OpenXR adapter uses zero for unavailable joints. The demo
  requires finite, nonzero data for all 18 needed positions before accepting a
  hand; index intermediate/distal are visual-only. This avoids feeding missing
  joints at the origin into the fit. No substitute fingertips are synthesized.
- This display visualizes selection; it has no interactive tool actions.
  Missing hands hide their diagnostics. Frame rate, runtime tracking quality,
  comfort and subjective feel need actual headset feedback.

Official setup references:
[Unity XR Hands 1.3](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.3/manual/index.html)
and [Hand Tracking feature](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.3/manual/features/handtracking.html).


Version 0.2 built and installed successfully on 2026-09-25 (ARM64; SHA256 9333F7D8CBD90F644B920B9D5563076ACA8247A1C6A07B567DBC4D1C5F2A2FEF). Its new palm/depth feel awaits Dana's return; v0.1 received the earlier small-gap report.

Version 0.3 is now installed (2026-09-25): attached trail-tip fix, APK SHA256 4C126AE2BDFE82A25B8217E3B6FDDA6A7D49EBE0AAEB6542EC9A841D959FB195. Earlier deployment entries describe historical versions.

Version 0.4 is now installed (2026-09-25): integrated outward growth, APK SHA256 B95B426B42A9109A9E18D75450010BE4E4570818168930D1FFB49C734F979138. Physical feel remains deferred.


Version 0.5 built and installed successfully (2026-09-26 UTC), replacing the physically problematic sphere cap with the pose-aware two-law experiment and adding optional local joint recording. APK SHA256 1559DDD34617A2300561D1EBCD5251A076FE53D0FA501A3FEB79CBA8CE04C57A. Both actual C# and compiled-Udon hand-limit checks pass; physical v0.5 feel remains pending.

Version 0.6 built and installed successfully (2026-09-26 UTC): hand-frame knuckle tilt, preferred plain Inflate default and house/vista scale references. APK SHA256 C8091C82D5354DB6CA7FEBB74EE2684A13E256831CBB769ECA7CC7D98C313D05. Private v0.5 replay, C#/Udon geometry checks and six editor render captures pass; physical v0.6 feel remains to be assessed.

Version 0.7 built and installed successfully (2026-09-26 UTC): restored closed-hand lever clicks, stronger far visuals and a high overlook; accepted v0.6 point/range/filter math retained. APK SHA256 6159DB04B06BB8533AC66AD0FD2B4F67F9CC03EB11CA563A77D53F9FAE4CF8CA. Real C#/compiled-Udon checks, eight vista renders and rapid-return checks pass. Physical v0.7 assessment is pending.


### v0.8 rendering checkpoint

Built and installed 2026-09-26: logical world depth, disjoint vista surfaces,
5 cm camera near clip, inflation reduced to 9.5x. Accepted geometry/click/filter
unchanged. APK 20,081,200 bytes, SHA256
`13F86D5A590552AD84A4FB7580E64F1531F36023955F1D0E1B292B0425B85C91`.
Package v0.8/arm64 and app/OpenXR Hands startup verified; no physical v0.8
assessment yet. See CHECKPOINT.md and DEPTH-STEREO-OCCLUSION.md for render evidence.
