# Quest development power settings

Latest device state, **2026-09-29, inspected 11:09 UTC**: latest LookoutRail01
production is hash-verified, visibly rendering normal arrival in actual Quest
VRChat, and **left running**. Earlier dim-room failure recovered by a normal
launch. Root and critic also inspected the lookout rail in both eyes and the
prior Label01 water-garden label from its dedicated inspection stance. Production
was restored afterward. Fixed tilt/fallback avatar remain; no physical hand,
comfort, client walking or multiplayer acceptance. Avatar click AND teleport
gates stay off. Battery 77%, AC, no weak-charger flag, 40 C. No boundary/tracking
bypass or account change. See newest CHECKPOINT and heavy
`Reference/WorldBuildingReviews/20260929-LookoutRail01` for sanitized results.
Raw device media/logs stay private. This supersedes the blocked state below.

Earlier device history follows.

Latest device state, **2026-09-29 09:02 UTC**: Label01 production is installed
and SHA256 verified, but **not confirmed loaded or running**. Quest shows Meta's
"Finding position in room" / room-too-dim tracking dialog; no VRChat process
remained after the normal production launch. This supersedes older live-world
statements below. Earlier entirely black captures exposed this dialog after an
ordinary sleep/wake cycle and warm app focus. Root and independent critic
inspected the stereo evidence. Do not bypass tracking/boundary or infer success
from transferred files/process presence. Retry when physical room tracking
recovers; independent world development can continue. See newest CHECKPOINT and
heavy `Reference/WorldBuildingReviews/20260929-Label01` for sanitized evidence.
The latest battery check was 77%, AC powered, weak charger false, 41 C.

Earlier device history follows.

Current installed test world, 2026-09-28 UTC: **Social Bird 02** is transferred/
hash-verified and previously visibly rendered in actual Quest VRChat. At the
10:41 UTC recheck, its fresh cold launch was blocked by Quest's **“Finding
position in room” / room too dim** dialog. No VRChat process was running in the
10:42 UTC diagnostic. Do not claim it is currently rendering. The exact same
bundle remains installed; no new runtime was built this cycle. It contains personal Bird, debounced
pedestal acquisition and the optional social presentation layer. The previous
Personal Bird 01 was accepted by Dana; its near feel and visual inflation remain
unchanged. See the latest CHECKPOINT and private heavy
`Validation/CoastalWorld/DeviceSocialBird02` evidence. This unattended check
establishes loading/rendering, not physical debounce feel or multiplayer success.

Dana explicitly dedicates this headset to development and asks for extensive use
throughout the effort, including real VRChat validation. **The loaded build may
be replaced at any time without asking again.** This supersedes all earlier
instructions to preserve a particular session. Use it routinely for meaningful
checks; an unavailable device must not block independent development. At this
deployment it reported 54% battery, AC power, no weak charger and 41 degrees C.
The later diagnostic reports 69%, AC powered, no weak charger and 42 degrees C.
Private evidence for the failed launch and inspected stereo tracking dialog is
under `Validation/CoastalWorld/DeviceSocialBird02-20260928-Recheck` and
`DeviceSocialBird02-20260928-Diagnostic`. The new repeatable helper correctly
returns a failure even though transfer and the Android launch request succeeded.
It retains screenshot/startup evidence when the app process is absent. Resume a
normal client check once room lighting/tracking is available; do not disable the
boundary or substitute fake tracking to report a pass. Independent world work
continues while this physical environment condition remains.

## Wireless ADB and wall power, 2026-09-28 UTC

### Repeatable VRChat evidence

`tests/quest_vrchat_smoke.py` uses only Python's standard library and the installed
ADB executable. Use a new **ignored/private** output directory for every run;
raw device logs may contain account/session information and must not be committed.
With no bundle argument it captures the running session without restarting it:

```powershell
python tests/quest_vrchat_smoke.py --adb <adb.exe> --serial <authorized-device> --out <private-new-directory>
```

Adding `--bundle <validated-Android.vrcw> --world-name <new-build-name>` opts into
transfer and cold launch through the unmodified client's normal TestWorlds path.
Both launch extras are strings, as in the SDK. A failed device hash check stops
before launch. It never reinstalls the client or changes account, boundary or
sleep settings. A Wi-Fi address reconnects automatically; an unauthorized or
unavailable device is a recorded failure, not a false pass.

The evidence includes battery/power, memory diagnostics, per-process recent logs,
and a stereo PNG whose signature, complete chunks, CRCs and end marker are checked
before saving. ADB has previously returned success with a truncated screenshot,
so its exit code alone is insufficient. Inspect `headset.png` afterward: the
script explicitly leaves visual acceptance unverified. A successful launch
request does not establish the loaded world, and zero matched Udon error lines
does not prove absence of every device problem. Physical feel and multiplayer
acceptance always require their own evidence.

The Quest ran out of battery on computer USB power. A power/data adapter attempt
did not expose an ADB device; Windows had an unidentified USB descriptor failure,
which did not establish the failing device's identity. Reconnecting directly
restored authorized USB ADB. Battery service reported 17%, `Weak Charger=true`,
and a 5 V / 0.9 A charging limit (4.5 W).

Wireless ADB was enabled through the authorized USB connection using
`adb -s <USB serial> tcpip 5555`, then connected with
`adb connect <headset Wi-Fi address>:5555`. Explicitly addressed Wi-Fi shell
commands continued working after USB removal. Lab 14's existing device bundle
SHA256 still matched its validated artifact; no app was reinstalled/restarted.
The device's current LAN address is saved only in local ignored device evidence,
not assumed permanent. A DHCP/address change requires rediscovery; a reboot may
require briefly reconnecting USB to enable TCP ADB again.

After Dana moved it to a wall charger, battery service reported AC power,
charging status, `Weak Charger=false`, and a 9 V / 3 A maximum charging limit
(27 W ceiling, **not measured instantaneous draw**). At 06:44 UTC its charge
rose from the observed 14% low to 16% while awake and reachable over Wi-Fi.
It subsequently reached 23%; no VRChat process was running during that check.
This demonstrates awake-idle recovery, not an indefinite/full-VRChat-load
power guarantee. The proximity override had reset during the power loss and was
reapplied under Dana's standing authorization. Plugged-in stay-awake remains 15,
covering AC as well as USB; `mStayOn=true` and `mWakefulness=Awake` were verified
on wall power. Persistent `disable_autosleep` remains false.

For future device work, reconnect with `adb connect` if a Unity/ADB lifecycle
leaves the Wi-Fi transport absent, then use `adb -s <address>:5555` explicitly.
This happened during local editor runs; the headset still accepted reconnection
without another USB handshake. The exact host-process cause was not established.
Do not mistake loss of the host transport list for loss of the headset's TCP mode.
When USB and Wi-Fi both appear, they can represent the same physical Quest; avoid
unqualified commands that fail with multiple-device ambiguity. Keep hardware
optional for independent work. Earlier session-preservation requests are
superseded by the standing authorization above.

This follows Android's documented
[ADB-over-Wi-Fi workflow](https://developer.android.com/tools/adb#wireless).

## Earlier sleep and lab history

Dana explicitly authorizes disabling headset sleep through ADB (2026-09-27
local time). The connected Quest 3 accepted these development settings:

```powershell
adb shell svc power stayon true
adb shell am broadcast -a com.oculus.vrpowermanager.prox_close
```

Verification: `dumpsys power` reports `mStayOn=true`,
`mStayOnWhilePluggedInSetting=15`, and `mWakefulness=Awake` while USB-powered.
`content call --uri content://com.oculus.rc --method GET_PROPERTY` reports
`set_proximity_close=true`. The existing screen timeout remains 86400000 ms
(24 hours) and secure sleep_timeout remains -1. No boundary or dialog settings
were changed. This is practical USB development wakefulness, not proof that
all sleep mechanisms are permanently disabled on battery.

Meta's proximity override does not survive reboot; it may be reapplied during
authorized development. To restore the physical sensor, broadcast
`com.oculus.vrpowermanager.automation_disable`. Restore the previous plugged-in
setting with `adb shell svc power stayon false`.

The separate persistent `disable_autosleep` property remains false. Its SET
request without a PIN returned `Success=false`, `No PIN specified`. Do not
guess the PIN or claim that permanent switch succeeded. No PIN was requested
because the other settings suffice for the current session.

Sources: [Meta's proximity-override documentation](https://developers.meta.com/horizon/documentation/unity/meta-xr-operator/quest/)
and [Meta's scriptable testing settings](https://developers.meta.com/horizon/documentation/native/android/ts-scriptable-testing/).

Lab 12 was relaunched from its already validated, hash-matching TestWorlds
bundle using the Android activity and `localWorldPath`/`watchWorlds` extras
found in the installed unmodified VRChat SDK. Its 2026-09-28 UTC live stereo
capture is kept privately under heavy-repository
`Validation/TrackingLab/DeviceLab12/LiveRetest`; the Lab 12 title and tracked
hand markers are visible. No online upload occurred.

Dana subsequently requested automatic startup with no SET actions. Lab 13 is
was deployed as the next Quest test world, replacing Lab 12. Its validated Android bundle
is 273127 bytes, SHA256
`07DA7B94BAD4BB0C43FC62473ADEDEF18272DDD5052AA420D8B8241DE11BB68F`,
stored in the same app's TestWorlds directory as
`BirdTrackingLab13_Automatic.vrcw`. A fresh stereo capture confirms the Lab 13
title and automatic-start instructions. Both hands report 16/16 avatar bones.
Private evidence is in `Validation/TrackingLab/DeviceLab13`. Geometric estimates
are available before passive learning; REFINE/AUTO are optional corrections.
Physical feel remains unconfirmed; do not mistake scene rendering for that check.

Latest device checkpoint, 2026-09-28 UTC: Lab 14 now replaces Lab 13 on Quest.
The additional sphere-center Kalman trial retains automatic startup and the
accepted nearby path. Android artifact: 280263 bytes, SHA256
E0D0F737748F4176A6599F6698C0F6A3A244E6F8470CC75A14668022A3263BC4.
Device file: BirdTrackingLab14_CenterKalman.vrcw in the same TestWorlds directory.
Transfer hash and actual Lab 14 stereo rendering are verified; both hands show
16/16 avatar bones. Physical feel is pending. Prior Lab 13 remains for rollback.
See CENTER-KALMAN-LAB.md for the large-turn range-contraction tradeoff.

Latest observed device state, 2026-09-28 12:54 UTC: the earlier dim-room warning
has cleared. VRChat Home was rendering before deployment. The validated Coastal
R06 Android world then transferred with matching SHA256
`46BA39ABAB4B9D91FB82CE80CF2AD7103E06A18388A8E342F45F91591462D124`,
and a normal cold launch loaded `BirdCoastalWorld_R06.vrcw`. The initial capture
showed initialization; the subsequent inspected stereo image shows the coastal
arrival and Bird pedestal. It now replaces Social Bird 02 as the observed live
world. Both older files remain available for rollback; no online upload occurred.

Private evidence is under `Validation/CoastalWorld/DeviceR06-20260928-*` in the
heavy repository. One capture failed with an ADB transport exit while Unity was
switching build targets; a fresh capture-only retry succeeded without relaunching
the app. The checked recent client log has no matched Udon exception, but does
contain platform/voice startup warnings and Oculus transaction errors. Do not
describe the whole log as clean. Battery was 73%, AC powered, weak-charger false,
43 C at the successful post-load observation. This proves transfer/loading and
actual rendering, not physical navigation, hand feel, multiplayer or whole-world
performance acceptance. Use the dedicated device routinely after meaningful
changes under Dana's replace-anytime authorization.
