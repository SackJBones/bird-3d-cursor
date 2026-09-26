# Hanoi Preview

Import this sample through Package Manager, including `Resources`. Add
`BirdHanoiPreview` to an empty GameObject in a separate empty scene and enter
Play Mode. It creates a small tabletop puzzle and a distant puzzle made of
building sections, using the same manipulation components at 375 times the scale.

Desktop controls:

- Mouse position aims; the wheel changes the synthetic Bird point's reach.
- Hold the left mouse button through a top piece to pick it up, then move it.
- Follow a visible approach guide toward a legal dock. Release when the dock
  feedback indicates readiness. A larger piece cannot go on a smaller one.
- Right mouse or Escape cancels and returns the piece. R resets both puzzles.

The goal is to move the complete stack from dock 1 to dock 3. Three pieces can
be solved in seven moves. Invalid drops and tracking loss return to the last
committed position without changing the move count. Closing a hand cannot pull
a distant building outside its authored work area toward the viewer.

For a host that supplies accepted Bird samples, assign `externalLeft` and
`externalRight`, turn off `desktopInput`, and optionally turn off `createCamera`
before initialization. Submit to those `BirdPointerInput` components from the
host and cancel them on tracking loss. The preview creates its own small
environment; a production host should author a layout using the underlying
components. Disable/remove the preview to clean up its generated objects and
materials; unrelated children and external inputs are preserved.

Runtime components live in `Bird3D.Manipulation`, outside the solver and editor
assemblies. `BirdHanoiBoard` is a sample placement rule, not a geometric Bird law.
Inspector references and Grabbed/Placed/Cancelled UnityEvents support conventional
authoring. Box volumes must enclose the intended object and start inside their
placement region. Distances are region-local; changing the region's scale scales
the whole interaction. Use one driver for the registered pointers/objects and
configure outside an active transaction.

This is translation and allowed-destination placement, not physical collision
avoidance or an automatically networked puzzle. Pieces can pass through one
another while moving. The host must keep the user's movement area outside a
distant building workspace. Menu and world input arbitration, rotation/scaling,
Udon adaptation, VRChat-client and physical hand testing remain future work.

Measured on Unity 2022.3.22f1 / D3D11: 1216 editor assertions, six camera captures,
and a Windows standalone build/runtime/render pass with synthetic input. See the
repository's `docs/modernization/OBJECT-MANIPULATION.md` for contracts, limitations
and the reproducible test runner. This sample is not installed on Quest by importing it.
