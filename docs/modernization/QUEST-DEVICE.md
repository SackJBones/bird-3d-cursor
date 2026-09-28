# Quest development power settings

Current world, 2026-09-28 UTC: Dana requested the coastal world if runnable.
Coastal R05 is now transferred/hash-verified over Wi-Fi and visibly rendering in
Quest VRChat. It is left open for exploration; Lab 14 remains installed but is
not selected. This is an architecture-only preview, with Bird interaction still
in the lab. See latest CHECKPOINT and private heavy
`Validation/CoastalWorld/DeviceR05` evidence. Preserve the current user session.

## Wireless ADB and wall power, 2026-09-28 UTC

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
optional for independent work and preserve Lab 14 while awaiting feel feedback.

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
