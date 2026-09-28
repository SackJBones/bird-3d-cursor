# Quest development power settings

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
now the active Quest test world, replacing Lab 12. Its validated Android bundle
is 273127 bytes, SHA256
`07DA7B94BAD4BB0C43FC62473ADEDEF18272DDD5052AA420D8B8241DE11BB68F`,
stored in the same app's TestWorlds directory as
`BirdTrackingLab13_Automatic.vrcw`. A fresh stereo capture confirms the Lab 13
title and automatic-start instructions. Both hands report 16/16 avatar bones.
Private evidence is in `Validation/TrackingLab/DeviceLab13`. Geometric estimates
are available before passive learning; REFINE/AUTO are optional corrections.
Physical feel remains unconfirmed; do not mistake scene rendering for that check.
