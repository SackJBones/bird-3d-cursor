# Knuckle-directed reach and a scale-reference setting

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
