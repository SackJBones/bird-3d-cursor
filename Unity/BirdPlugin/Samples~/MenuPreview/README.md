# Menu preview

Import **Menu Preview** from Package Manager's Samples section. In an empty scene, add
`BirdMenuPreview` to an empty GameObject, disable the default camera, and enter Play
Mode. This desktop example creates its own camera and uses the Built-in render
pipeline's `Unlit/Color` shader. Set Active Input Handling to **Input Manager (Old)**
or **Both** for its mouse and keyboard controls.

Import the complete sample, including its small `Resources/BirdMenuPreviewSurface`
material. This explicit asset keeps the Built-in shader in standalone builds.
The preview clones that material for its visuals and cleans up its generated
objects/materials when removed. Disabling the preview hides its camera and controls.

- **G:** demonstrate an upward Bird sweep through the opening bar.
- **Mouse:** aim. **Mouse wheel:** move the logical point nearer or farther.
- **Left button:** select. Reach through COLORS, click, then choose CYAN or AMBER.
- **BACK:** return to the parent. **CLOSE / Escape:** close the menu.

The dim parent retains its place while its child has focus. The sphere on the right
shows the selected color. These are ordinary `BirdMenuPanel`, `BirdMenuElement`,
`BirdMenuInteractor` and optional `BirdMenuFeedback` components connected with
UnityEvents. The runtime scene builder is an example, not a required authoring API.

The synthetic source feeds logical points directly; it does not simulate hand
anatomy, evaluate Bird geometry, or open a tracking device. The reusable components
live in the independent `Bird3D.UI` assembly. A tracking adapter supplies an origin,
the actual Bird point, tracking validity and the current pressed level:

```csharp
pointer.Submit(handRoot, birdPoint, tracked, pressed);
```

Submit once per input update before the interactor's `LateUpdate`. Call `Cancel()`
when the source stops supplying data; a silent source has no automatic timeout.
Both hands should share a user ID and use distinct input components. A newly
acquired held press must be released before it can activate a menu.

For Inspector authoring, keep panel controllers outside their hidden **Content**
children. Put button visuals and a separate Box/Sphere/Capsule/convex Mesh collider
under Content, assign the owning panel and target collider to each element, and
choose its activation mode. Connect **Activated** to the dynamic
`BirdMenuPanel.Open(BirdPointerInput)` method, a panel's parameterless `Close()`, or
your own action. Set a child panel's Parent explicitly. Register all elements,
including inactive children, on one interactor with both pointer inputs. Its
**Refresh Child Menu Elements** context command fills the element list.

## Spherical selector

For the spherical example, attach `BirdSphericalSelectorPreview` in a separate
empty scene instead. Mouse movement aims, the wheel changes reach, and a click
chooses a color the logical point reaches through. Move the point beyond the
**back** of the larger wire sphere to scroll, then withdraw inside to let the
choices coast. Movement through the front or inside the sphere never drives
rotation. No click or hold is needed for scrolling.

This example has twelve color orbs, a dodecahedral wireframe and an oversized
interaction sphere. Particle fireball art is not yet reproduced. Rotation uses
`BirdSphericalScroll`, placement uses `BirdDodecahedronLayout`, and color actions
use ordinary `BirdMenuElement` events. The example's **Color Selected** UnityEvent
can connect to another experience; its default action changes the result sphere.

To embed the selector in a live host, call `Initialize(pointerInputs)` before its
Start callback. This supplies externally owned logical inputs, disables desktop
input, and creates no camera or extra Bird marker. The example uses local scene
coordinates; position/rotate its root for the desired placement. Supply both hands
with a shared user ID. The Quest comparison host now uses this path directly.

Ordinary point-through highlighting is distinct from the back-surface scrolling
gate. The reusable scroll component also accepts an optional owning menu panel.
Udon/VRChat action dispatch and headset usability remain separate integration work.
