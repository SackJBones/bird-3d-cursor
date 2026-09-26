# Menu artwork states

`BirdMenuVisual` extends the optional presentation of a `BirdMenuElement`. Its
Udon counterpart is `BirdUiVisual`. Both consume the existing five-state result
without changing hit tests, focus, click edges, actions or Bird's geometric point.
This recovers the useful state-pose authoring from `BaseMenuElement` through
composed Unity components, without importing DOTween or the archived inheritance
structure. A mandala or other experience does not need this UI presentation.

## Scene and prefab authoring

Use a stationary control with its collider, element and visual component. Create
a separate child for body meshes and labels, assign it as Visual Root, and assign
an optional renderer within that branch for tint. No collider, menu element or
visual controller may live in the animated branch, even disabled. An ancestor of
the source/controller is also invalid. Runtime checks reject these hierarchies
and restore the previous artwork; the ordinary Unity Inspector shows a warning.
The authored `BirdMapDemo` demonstrates this arrangement on eleven boxed controls.
Color orbs retain their existing independent selection and scrolling presentation.

Each state has position offset, Euler rotation offset, scale multiplier, tint and
visibility. Ordinary Unity uses serializable `BirdMenuAppearance` foldouts; Udon
uses explicitly named Inspector fields under state headers, avoiding parallel
arrays or unsupported nested custom serialization. Both support normal scene and
prefab references. The ordinary custom Inspector uses default serialized editing
and Undo; it does not alter artwork in Edit Mode or require a custom save command.

| State | Meaning supplied by the element |
| --- | --- |
| Inactive | Source unavailable or its menu branch closed |
| Enabled | Available without current hover |
| Highlighted | Eligible pointer targeting the element |
| Activated | Pressed presentation while targeting the element |
| Background | Parent branch remains visible behind its focused child |

Activated is a **pressed level**, not proof that a new activation event occurred.
For example, tracking recovery with a held press can show it while the input
correctly suppresses a new click edge. A persistent selected color/mode belongs
to the experience model, not this transient state. Actions continue to use the
element's UnityEvent or Udon action component; rendering never invokes them.

Offsets are relative to the artwork's local transform captured on binding.
Position uses its parent's axes; rotation is `restRotation * Euler(offset)`;
scale multiplies each rest-scale component. Nonnegative multipliers preserve a
mirrored rest transform rather than creating a new reflection. Rest scale may be
nonuniform. Parent/world motion does not change these local authoring semantics.

The optional renderer uses a MaterialPropertyBlock and a configurable color
property (`_Color` for the sample Built-in shader). Shared materials remain intact.
Other renderers/labels move with the branch but are not automatically tinted.
Give one component ownership of the branch and the renderer's block. Disable
`BirdMenuFeedback` or other writers on that target before binding. The complete
original block, including unrelated properties, is retained and restored.

## Transition and lifecycle contract

The first bind immediately displays the current state. Later transitions use a
finite duration (default 0.14 seconds), smoothstep position/scale/color and
quaternion slerp. A new state retargets from the currently displayed values; no
animation queue accumulates. A zero duration is immediate. Automatic updates use
unscaled elapsed time after interaction routing. This is a visual response law,
separate from the geometric Kalman filter and range/scroll dynamics.

Showing activates the artwork immediately. Hiding deactivates it only at the end
of its transition. Controllers outside the branch remain able to show it again.
A parent panel that disables its whole Content branch necessarily hides children
immediately; this component cannot animate an inactive hierarchy on screen.

Disabling or rebinding restores the original transform, activeSelf and full
property block. Restore clears ownership before SetActive can trigger callbacks.
Explicit `Restore()` releases the current binding; a still-enabled automatic
component binds again on its next update. Disable it to stop driving permanently.
Invalid hierarchy, nonfinite pose/output or negative scale multipliers restore
the artwork. Negative/nonfinite elapsed time is ignored without advancing it.

Ordinary scripts can use `Configure`, `SetAppearance`, `GetAppearance`,
`SetTransition` and `Apply(dt)`. Appearance setters/getters copy values; the source
object cannot silently mutate the stored style. Configure belongs between visual
updates. Udon exposes grouped fields plus `Initialize`, `Refresh` and `Process`;
after changing fields at runtime, send Refresh to retarget a held state. For
caller-timed Udon updates set automatic=false, assign stepDelta, then send Process.

`BirdVisualStatesPreview` is an optional one-time builder on `BirdMenuPreview`.
It replaces the sample's simple feedback with a small hover advance/tilt, pressed
compression and background recession. The base preview owns its generated objects
and materials. Removing/disabling the builder alone does not undo its augmentation;
disable the generated visual components to restore their captured rest artwork.
The sample's desktop controls and existing action callbacks are unchanged.

## Evidence and reproduction, 2026-09-26

Use the two visual runners documented in [tests/README.md](../../tests/README.md).
Final evidence is from Unity 2022.3.22f1/D3D11 and Worlds SDK 3.10.5:

- 49 actual Unity assertions: half-duration/retargeted poses, action separation,
  nested focus, loss/recovery, visibility, disable/rebind, property-block and
  shared-material preservation, invalid settings/geometry, extreme finite tint,
  transformed/mirrored/nonuniform frames and prefab references/styles/events.
- Five ordinary editor state captures and a Windows x64 Mono build. Its real
  Update/LateUpdate player passes hover, press, background, tracking loss, fixed
  bounds and single-action checks with two renders. UI is included; editor code
  is excluded. Inputs are synthetic logical points.
- 35 compiled-Udon assertions, including a normal-frame nested-menu sequence,
  VM-driven contract checks and four camera captures. Tests access backing VM
  heaps/events, not runtime C# proxies. The existing map regression runs afterward.
- Migration independently compares the previous committed scene against the new
  one: all 88 world-space corners of eleven boxed controls agree within 0.01 mm,
  including inactive content. The comparison does not rely on Collider.bounds,
  which is empty for inactive objects. Historical corner data is retained under
  [measurements](measurements/visual-original-hit-corners.csv).
- Synthetic 30/72/120 Hz fixtures sample the same 0.1 seconds of a 0.4-second
  transition and match the analytic smoothstep fraction 0.15625. The 72 Hz fixture
  includes a final fractional interval. Results are in
  [ordinary Unity](measurements/visual-state-rates.csv) and
  [compiled Udon](measurements/udon-visual-state-rates.csv) CSV files.

The migration is explicit and skips already-styled controls. Normal runs reopen
the authored scene without regenerating it. Stable source GUIDs and a tracked
Udon program asset allow fresh-checkout restoration into ignored generated folders.
Generated bytecode, projects, captures, logs and builds remain outside Git.

Physical feel, headset cost, mixed input/render cadence and VRChat-client use are
not established by these checks. The installed standalone Quest app remains v0.9.
The Windows SDK artifact/catalog result and its existing internal build-log
qualification are recorded in CHECKPOINT.md; no upload or client launch occurs.
