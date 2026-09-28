# Social Bird presentation

`BirdSocialPresentation` is an optional observer layer on the personal station's
`VRCPlayerObject` template. The normal SDK creates an independently owned copy
for every visitor. There is no shared pickup pool or ownership contest. Do not
add `VRCEnablePersistence`: transient hand points should not survive a visit.

The owner's instance reads the local station and sends complete two-hand
snapshots. Other copies feed two passive `BirdCursorState` containers and the
same `BirdPointPresentation` embodiment. These containers have no input adapter,
sphere fitter or filters and cannot click. The owner's duplicate observer views
remain hidden. Nothing in this component writes back to local input, geometry,
filtering or range. The normal local presentation remains immediate.

## Wire contract and lifecycle

- Manual sync: visible-hand bitmask, four world-space Vector3s (two palm origins
  and two logical points), two history revisions, sample timestamp and sequence.
  Nine fields contain **72 raw scalar bytes**, before SDK/network overhead. This
  is a field-size calculation, not a measured packet size or bandwidth guarantee.
- Default maximum request rate is 10 Hz while a hand is visible. The sender
  samples again in `OnPreSerialization`, so queued work uses current state.
  Congestion pauses new requests. Failed sends retry; an acknowledged hidden
  state is idle until a transition or join. Actual transport rate is SDK-owned.
- Receivers reject duplicate/out-of-order snapshots, invalid masks, non-finite
  or numerically unreasonable points and expired timestamps. One invalid hand
  does not discard a valid other hand. No usable update for 1.5 seconds hides
  both cursors and clears their trails. A fresh complete packet restores them.
- A first point or discontinuity appears immediately. Ordinary remote movement
  uses a bounded 0.1-second world-space interpolation. This is observer-only
  visual interpolation, with no extrapolation and no claim to reconstruct
  unobserved motion. Large turns can cut a chord between network samples; assess
  this in a real two-client session before tuning the remote effect.
- Loss, revision changes and palm jumps over 2 m start fresh trails. PlayerObject
  departure removes the entire owner's observer object. A late join uses a
  complete fresh snapshot; active owners keep publishing even while still.

Each player's ID determines a hue pair for the instance. Left/right are
contrasting companion colors (first pair cyan/pink); both hues rotate together
for subsequent players. This is not persistent identity, a globally unique
color assignment or a substitute for later accessibility/explicit palette UI.
It does preserve Dana's requirement to distinguish the two hands clearly.

## Authoring and validation

`BirdSocialPresentationAuthoring.AddToCoastalWorld` is a one-time additive prefab
migration. Use `Invoke-UnityCoastalWorld.ps1 -AddSocial` only before that template
exists. It never regenerates the architectural scene. Subsequent changes use
ordinary prefab editing. Runtime input references point outside the template to
the scene's local station; observer references remain inside the cloned template.

Run `-CheckSocial -CheckBird -Build` separately for Android and Windows. The
personal checks retain saved Lab 14 settings and verify body-exit pedestal
debounce plus rendering/occlusion through 10 km. The social checks exercise
actual ClientSim PlayerObject creation, initial ownership, reference remapping, palettes
and departure. Compiled Udon receives synthetic snapshots, including an SDK
field-codec roundtrip, to test stale/reordered/malformed data, loss, interpolation,
late arrival, put-away and recovery. The normal SDK export audit requires one
manual player stream and 17 unsynced programs, without persistence or project
MonoBehaviours. See CHECKPOINT for actual results and target hashes.

**ClientSim tests do not establish real multi-client transport or Quest cost.**
An attempted ownership-steal check found that SDK 3.10.5 ClientSim permits
`Networking.SetOwner` on these objects, unlike the official client contract.
The suite therefore checks initial ownership but does not claim to verify
immutability; test that in VRChat itself. No SDK code was modified.
Next social acceptance needs two clients in the same private world instance:
each acquires independently; each sees the other's contrasting hands/trails;
near/room-scale/10 km points agree; putting away, tracking loss and leave/rejoin
clear the right effects; a late join sees an already-active visitor; local
control remains immediate. Inspect actual serialization bytes/rate and device
frame time, then test more visitors before claiming full-capacity performance.
Use the dedicated Quest routinely; Dana authorizes replacing its loaded build
at any time. Social Bird 02 has been loaded through normal SDK TestWorlds, with
matching device hash and actual stereo rendering. The unattended spawn capture
does not establish touch acquisition, active remote cursors or multiplayer feel.

## Official SDK basis (checked 2026-09-28)

- [PlayerObjects](https://creators.vrchat.com/worlds/udon/persistence/player-object/):
  per-player templates, immutable ownership, runtime reference binding and
  optional persistence.
- [Networking events](https://creators.vrchat.com/worlds/udon/networking/network-components/):
  pre/post serialization, deserialization, congestion and timing APIs.
- [Networking specifications](https://creators.vrchat.com/worlds/udon/networking/network-details/):
  manual serialization limits and SDK rate limiting.
