# Bird menu components

The first maintained UI slice is in `Unity/BirdPlugin/Runtime/UI`, with an
importable **Menu Preview** sample. It implements directional opening,
point-through highlighting, selection, child/background focus, and back/close.
The ordinary Unity host uses serialized components and UnityEvents; no recovered
runtime scripts or DOTween internals were copied into the maintained package.

## Boundaries

`Bird3D.UI` is a separate runtime assembly with no Bird solver, hand SDK, or editor
reference. It receives the logical point through `BirdPointerInput`. It does not
change sphere fitting, range, filtering, click detection, visual projection or
cursor size. An experience can use the point without installing menu objects.

| Component | Responsibility |
| --- | --- |
| `BirdPointerInput` | Origin, logical point, tracking validity, pressed level, sample revision and stable user ID |
| `BirdMenuElement` | Collider hit policy, action priority, five visual states, serialized activation/state UnityEvents |
| `BirdMenuPanel` | Explicit parent/child branch, per-user focus, content visibility, open/close UnityEvents |
| `BirdMenuInteractor` | Evaluate registered inputs and controls, dispatch at most one action per new pointer sample, clear lost focus |
| `BirdMenuFeedback` | Optional eased Built-in `_Color` feedback using a MaterialPropertyBlock; never change collider size |
| `BirdMenuVisual` | Optional five-state artwork position, rotation, scale, color and visibility with finite-duration transitions; keeps hit geometry stationary |

For richer state styling and its separate Udon implementation, see
[visual-state authoring](UI-VISUAL-STATES.md). Use one visual owner per artwork
branch/renderer; the richer component replaces tint-only feedback on that target.

The archived `BaseMenuElement` supplied interaction requirements. Its inheritance
and animation implementation are not the authoring contract. The new sample is a
runtime builder for easy import; the same components can be attached and wired in
the Inspector. A real prefab save/reload check exercises a persistent dynamic
UnityEvent opening its remapped panel instance.

## Authoring and input contract

See the package's [Menu Preview instructions](../../Unity/BirdPlugin/Samples~/MenuPreview/README.md)
for setup and controls. A panel controller must stay outside its hidden Content
child. A root's opening gate also stays outside that child and has no owning panel.
Child panel relationships are explicit serialized references, independent of
Transform ancestry. Avoid parent cycles and assigning Content to its controller
or an ancestor; these assignments are guarded but are not meaningful layouts.

Register all controls, including inactive child controls, with one interactor.
Configure it before input starts; reconfigure between updates when the set changes.
Calling `Configure` from an action callback is rejected. Callbacks may close/open
panels or disable objects. All hit candidates are collected before actions run,
then eligibility is checked again at dispatch, so one hand closing a menu cancels
another hand's queued action in the closed branch. Ties go to the closest eligible
hit; higher explicit Priority overrides distance. Exact ties use registration order.
While replacing a child, attempts by that child's Close callbacks to open a
different sibling are ignored; this prevents an unregistered open branch. An
ordinary Open callback can still close its own panel.

Submit once per input update, before the interactor's `LateUpdate`:

```csharp
pointer.Submit(handRoot, logicalBirdPoint, validTracking, currentPressedLevel);
```

Both hands have distinct input components with the same user ID. This ID describes
local focus grouping, not network authentication. Neither hand can steal another
user's open branch. Losing one hand retains the owner's other hand; losing both
closes the branch by default. Set the root panel's Close When Focus Lost option to
false for an experience that explicitly manages persistent focus.

The producer must call `Cancel()` or submit `validTracking=false` when it stops
supplying samples, including on its own disable. There is no wall-clock timeout.
Nonfinite input, disabling the input component and changing its user cancel input.
Submitting while disabled cannot queue a pose for reenabling. A first/recovered
sample establishes a baseline: it cannot synthesize a click or sweep. Release an
already-held press before the next activation. No input queue exists: multiple
submissions before one interactor update would replace earlier edges.

Use the actual geometric point, not the distance-projected visual transform.
If a host moves colliders immediately before explicit `Process()`, synchronize
physics transforms before querying. The frame-driven host should similarly order
input/physics changes before routing. Do not register the same control in several
interactors. Feedback should own the target renderer's property block while enabled;
disable restores the block captured at binding and leaves the shared material intact.

## Hit and activation policies

Supported hit volumes are Box, Sphere, Capsule and convex Mesh colliders. Disabled,
missing and unsupported colliders are inactive. Use simple dedicated volumes for
animated menu visuals. A precise shape query rejects points merely inside a
rotated collider's world AABB.

| Policy | Activation |
| --- | --- |
| Select At Point | New press while the logical point is inside the target |
| Select Through | New press while the finite origin-to-point segment reaches the target |
| Enter Through | Transition into segment contact after a valid prior sample; no click needed |
| Directional Pass | Logical point motion enters the collider in the configured local direction/cone |

Hover is segment contact and is shared across eligible hands. Background controls
are dim and inactive unless Active In Background is explicitly enabled (for
example, a parent Back/Close action). There is no hardcoded 1000 m cast limit; the
actual finite point determines reach, subject to ordinary Unity float/physics
precision. The current menu policy queries registered controls individually; it
does not impose opaque-world occlusion. A future host can add a separate world
occlusion policy without changing the geometric point.

Spherical scrolling is supplied separately by `BirdSphericalScroll`. The
[spherical selector](SPHERICAL-SCROLL.md) uses an oversized sphere and
**back-surface** activation, leaving free movement and selection inside it. Its
scroll interaction, dodecahedral layout and selection are separate components.
Ordinary front/segment menu contact does not drive rotation.

## Reproduction and evidence

Run from the lightweight repository, with an installed Unity editor:

```powershell
./tests/Invoke-UnityMenuChecks.ps1 `
  -UnityEditor 'C:/Program Files/Unity/Hub/Editor/2022.3.22f1/Editor/Unity.exe' `
  -ProjectPath '../bird-3d-cursor-projects/Validation/Menu2022' `
  -BuildPlayer
```

The runner only creates or updates a project carrying its own validation marker.
It resets result files to PENDING, checks Unity exit status and final PASS, and
bounds editor/player execution. Graphics remain enabled for actual camera/pixel
checks. It saves an interactive `Assets/MenuPreview.unity`; the player build can
also run interactively without its smoke-test command argument.

Unity 2022.3.22f1 D3D11 Play Mode currently passes **57 assertions**, with seven
1280 x 800 camera captures. Checks include opening direction, rendered highlight,
selection edge suppression, nested/background/back/close behavior, two hands and
outsider focus, tracking loss/recovery, disabled input, actual collider geometry,
2000 m targets, priority/nearest arbitration, reentrant callbacks, property-block
restoration and persistent prefab events. Inspected open, submenu and selected
camera views; layout was adjusted to separate parent and child rows.

The Windows x64 Mono player also builds and passes a frame-driven smoke run using
normal `LateUpdate` routing: open, highlight, color action, back, loss cancellation
and removal of the generated preview objects. Its D3D11 selected-state camera
capture was inspected. The player includes `Bird3D.UI` and excludes the Bird editor
assembly. An initial build exposed stripping of the dynamically found Unlit shader;
the distributed sample now loads an explicit Resources material that retains it.
A focused failing editor regression also caught a reentrant sibling-open callback
leaving an orphan branch; the final guarded replacement passes that regression.

Generated scenes, builds, logs and PNGs remain in the ignored heavy validation
project. No physical input, headset, alternate graphics pipeline, multiplayer,
Udon/VRChat action adapter or new Unity 2020 compatibility result is claimed.
The first flat menu slice did not change Quest v0.8. The subsequent v0.9 harness
connects the spherical example to both accepted live point/click streams; see
SPHERICAL-SCROLL.md and QUEST-LIVE-HANDS.md for its separate build/deployment
evidence. The subsequent [Udon integration](UI-UDON.md) supplies separate local
menu/spherical components, a saved color station and compiled ClientSim checks.
The ordinary Unity scripts are not executable VRChat Udon. Map controls, richer
animation authoring and physical input validation remain separate milestones.
