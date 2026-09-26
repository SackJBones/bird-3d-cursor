# Knuckle-directed reach and a scale-reference setting

## Current v0.7 arrangement

Dana reports that the v0.6 range transition feels very good. Its hand-only
45-degree tilt, point geometry, polynomial and filtering are preserved.
The scene now looks out from a high house/terrace over a valley roughly 150 m
below. A lake, trees and full-scale buildings occupy the valley; nearby
mountain shoulders frame an open center, with more distant mountain layers.
The room sits two meters behind the initial eye's horizontal position so the
user starts closer to the opening. A shorter terrace exposes more lower
terrain. The nominal 1.65 m floor offset is unchanged. Height labels are gone.
World far clip is 5000 m and fog is lighter to retain distant silhouettes.
These are scene changes only; the Bird display shell is still independent.

Far presentation is stronger: 11x rather than 8x inflation, a larger/more
opaque locator with a dark edge, twice the far trail width and higher far
trail opacity. Near cursor and trail sizes remain fixed. The controlled
bright-vista comparison at 1600x1000/80-degree FOV detects 18 high-contrast
cyan pixels with the new profile versus zero with the old profile, using the
same scene/trajectory and strict RGB thresholds. Zero does not mean the old
cursor emitted no pixels; its coverage missed that contrast threshold.
Eight rendered captures cover the vista, cursor distances, old/new far style,
furniture and exterior. Inspected the final vista and far-style capture.
Headset perception and the known shell/world occlusion limitation remain
separate from this editor evidence.

The sections below preserve the original v0.6 design and replay evidence.

Dana physically tried v0.5, recorded 20 seconds of hand motion, and prefers
plain **Inflate** over fixed size or Inflate + lag. The full-open palm-normal
direction demanded an awkward wrist angle to aim forward/up. The requested
correction must depend only on the hand, including when upside down.

Version 0.6 adds `BirdCursorState.flatDirectionDegrees`, default 45 degrees.
The across-knuckles vector comes from index and little-finger bases. Crossing
it with the palm normal gives an in-plane forward axis; the thumb/root/knuckle
geometry resolves its sign. The far law uses
`normal*cos(angle) + knuckleForward*sin(angle)`. No head, torso or world-up
vector enters this computation. Zero restores v0.5 direction; 90 points along
the palm toward the fingers. Existing ordinary-fit, flat/closed blend, range
and filtering rules remain. The setting is an initial feel experiment, not an
anatomically universal or perceptually optimal angle.

## Recording and replay evidence

The local file contains 2880 rows over 19.998 seconds: 1440 tracked/valid right
hand samples and two tracked/valid left hand samples. Other left-hand rows are
untracked. The file remains in ignored `Validation/JointTraces`, outside Unity
Assets, public Git and the APK. The user confirmed capture and physical v0.5
operation; this supersedes the earlier inconclusive unattended startup check.

`UnityQuestReplayChecks` reconstructs the same canonical fit points and root
from the joints, runs current production geometry at zero and 45-degree tilt,
and independently calls original `Bird.cs` for the legacy sphere direction.
It checks recorded v0.5 range inputs at zero tilt, unchanged geometry when the
legacy contribution is complete, preserved flat-pose range, and rotated/upside-
down input equivariance. It does not reproduce unknown pre-recording filter
history, infer intended user motion or claim physical v0.6 validation.

A separate [temporal audit](HAND-RETURN-FILTER.md) now measures the recording's
filter history and return delays. It confirms severe lingering far-state on
ordinary returns; the numerical geometry passes below do not resolve that
behavior.

Measured in actual Unity:

- 1442 usable recorded samples; 661 with full legacy contribution, 560 fully
  flat according to the existing classifier.
- Zero-tilt v0.5 reproduction: maximum range-input difference 0.000020855 m.
- 96 rotated/translated replay cases: maximum vector error 0.0000219392 m.
- Flat-pose legacy tilt toward the knuckles: median 26.06 degrees. Mean full
  angular disagreement with legacy drops from 41.93 degrees for the old normal
  to 28.82 degrees with the candidate 45-degree tilt. Legacy becomes unstable
  in limiting poses; closeness to it is descriptive, not a universal target.

The shared synthetic suite also checks zero/45/90-degree settings, a down-and-
forward palm aiming above the horizon, upside-down hands, ordinary parity,
fist return, and invalid angle rejection in C# and compiled Udon.

Build with optional private replay:

```powershell
./tests/Invoke-UnityQuestHandsBuild.ps1 -UnityEditor '<Unity.exe>' `
  -ProjectPath '<generated QuestHands2022 directory>' `
  -JointTracePath '<local JSONL recording>'
```

Without `-JointTracePath`, the private replay is explicitly skipped. The
synthetic preflight still runs. Recordings are never copied into Assets.

## House and vista

`tests/UnityQuestVista.cs` is demo presentation, separate from hand geometry.
It makes a simple open-front house with a pitched roof, a terrace with
meter-spaced seams, a 0.75 m table, chair and book, garden/trees, a lake, three
building landmarks and distant hills. Buildings are 12/25/65 m tall at
30/100/300 m forward offsets; their labels are scene references, not dynamically
measured slant ranges. They are decorative scale references, not the planned
interactive Hanoi demo.

The house anchors once after head tracking becomes available, facing the
initial view. Its floor is placed 1.65 m below the initial eye position so both
device-origin and seated setups have a useful illustrative scale. This is not
a measured physical floor or locomotion/collision system. Head-based placement
is confined to the environment; it never changes Bird's geometric point.
Simple standard materials, one shadowless directional light and distance fog
provide shape/depth cues. The camera retains its 1000 m far plane and the
cursor's existing display shell.

Plain Inflate is now the default in both the demo and optional depth component;
all three comparison controls remain. The 32 mm near size through 4 m remains.
`UnityQuestVistaChecks.Run` renders six actual Unity views and checks two
physical scale references. Pictures establish scene composition, not headset
comfort or device performance. The previously measured far-shell occlusion
limitation still applies against distant world geometry and remains a separate
task; the environment does not fix it.


## v0.8: scenery depth and calmer inflation

The Quest scale harness remains a test environment, explicitly not the intended
VRChat Bird World. Preserve the accepted high-overlook arrangement while fixing
its rendering. Ground, lake and road now partition one planar mesh into disjoint
material regions; window bands replace facade cells rather than overlap a backing
wall. The camera near plane moves from 5 mm to 5 cm (world far remains 5 km),
improving ordinary depth precision by roughly tenfold at distant landmarks.
A mere centimeter offset would not reliably separate conventional-depth surfaces
hundreds of meters away with the old clip ratio. No extra height labels.

The optional marker inflation factor decreases 11 -> 9.5 (about 14% smaller at
and beyond 20 m). Far trail/locator salience remains; near 32 mm/2 mm sizes through
4 m and immediate return shrink remain. Logical-depth rendering lets Bird pass
behind the landscape; hiding its appearance does not move its geometric point.

Eight actual vista images and 48 moving-camera diagnostic color samples pass on
both D3D11 and OpenGLCore. The samples probe grass, lake, road and a building
window away from silhouette edges. No competing material appeared. Far salience
comparison was moved into visible sky because a correctly occluded cursor behind
the mountain must not count as a visibility failure. Old v0.6/new v0.8 profiles
yield 0/17 high-contrast cyan pixels in that specific fixture. This threshold is
not total emitted pixels or a perceptual study. Headset feel remains separate.
