# Desktop preview

Import **Desktop Preview** from this package's Samples section in Package Manager. In a new empty scene, add an empty GameObject, attach `BirdDesktopPreview`, and enter Play Mode. The component creates its own camera and visuals; remove the default scene camera or disable it for this preview. This initial preview targets the built-in render pipeline and uses the Unlit/Color shader.

Two independent Bird solvers consume synthetic non-anatomical sphere points. Cyan represents left and pink right. Radius controls the size of the point sphere and its distance from the synthetic hand root; the production solver computes each cursor. Animate orientation rotates the input. Index inside sphere simulates selection, increasing cursor size. Pose available simulates tracking loss: input points disappear, the cursor holds, and a held selection releases once. Counters count both hands, so a simultaneous press produces two edges.

These controls demonstrate solver behavior; they are not a substitute for real tracking, a calibrated hand model, BirdProvider/Interactable integration, or VRChat. No device is opened. No custom shaders, textures or external SDKs are needed. The generated scene and any future bulky art belong in the demo repository.
