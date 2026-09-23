# Desktop preview

Import **Desktop Preview** from this package's Samples section in Package Manager. In a new empty scene, add an empty GameObject, attach `BirdDesktopPreview`, and enter Play Mode. The component creates its own camera and visuals; remove the default scene camera or disable it for this preview. This initial preview targets the built-in render pipeline and uses the Unlit/Color shader.

Two independent Bird solvers consume synthetic non-anatomical sphere points. Cyan represents left and pink right. Radius controls the size of the point sphere and its distance from the synthetic hand root; the production solver computes each cursor. Animate orientation rotates the input. Index inside sphere simulates selection, increasing cursor size. Pose available simulates tracking loss: input points disappear, the cursor holds, and a held selection releases once. Counters count both hands, so a simultaneous press produces two edges.

These controls demonstrate solver behavior; they are not a substitute for real tracking, a calibrated hand model, BirdProvider/Interactable integration, or VRChat. No device is opened. No custom shaders, textures or external SDKs are needed. The generated scene and any future bulky art belong in the demo repository.

Automated headless Unity Play Mode checks exercise both cursors, selection, radius changes, tracking-loss/recovery visibility and cleanup. Removing the component removes its generated camera, geometry and materials, while leaving unrelated children untouched. The camera view has also been rendered and inspected at 1280 by 720 with Direct3D 11 in idle, selected and lost-pose states. GUI layout/clicks, other aspect ratios and standalone rendering remain unverified.

Each cursor now leaves a short fading trail, capped at 128 points/2.5 seconds. Clear trails removes both strokes, and losing the synthetic pose clears them to avoid connecting across reacquisition. The preview owns a shared Sprites/Default trail material; its cleanup is covered by the Play Mode checks. A fourth camera capture, preview-trails.png, demonstrates both trails after animated synthetic input.
