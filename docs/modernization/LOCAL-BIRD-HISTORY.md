# Recovered local Bird history — 2026-09-26

## Result

The mature Bird menu system was present locally but missing from the maintained
source. Three read-only exploration agents searched distinct project groups,
hydrating small source/settings/scene files as needed. All targeted reads
succeeded after occasional initial Dropbox delay. No original was edited, no
historical Unity project was launched, and no full project was synchronized.

The strongest reference is:
`C:/Users/Dana/MIT Dropbox/Dana Gretton/Unity/bird-3d-cursor-mac/BirdFireballDemo`.
It has a complete, scene-wired `BaseMenuElement` family. Git HEAD there is
`f6d2c8b` (2023-07-14, “janky but working block tower restacking”); the menu
runtime, editor and PointerDynamics files are present on disk but absent from
`git ls-files`. Their later work was therefore not preserved in that commit.

Dana clarified that the multi-demo launcher may exist only on their partner's
computer, and that the recovered menu capabilities are sufficient: that later
project mainly used this system rather than expanding it. Do not keep hunting
for the absent launcher as a prerequisite to implementation.

## Approximate timeline

| Period | Local project/source | What it adds |
| --- | --- | --- |
| 2018–early 2019 | MIT Unity `SphereFit`, `HandFit`; E: cold-storage `BirdManager.cs` | Serial glove, sphere fitting, hand smoothing and geometry experiments; unfinished code in places. |
| 2018–2019 | `E:/Cold Storage/Old desktop contents/` MoonshotDemo and Aubrey/PreVT/BirdUserTest variants | Older worlds and Bird/Reel/HOMER/GoGo user-test work. |
| 2021–April 2022 | MIT Unity `w i g g l i h a n d`, `Bird_UI_Dev` | Tracked-hand interaction/UDP and simple hand/sphere UI experiments. No scene launcher in the present snapshots. |
| April–May 2022 | MIT Unity `BirdSphereDemo`, `bird-3d-cursor-old-bad` | Interaction-option stations with beacons and teleport/reload events; single-scene showcase. |
| July 2023–July 2024 | MIT Unity `bird-3d-cursor-mac/BirdFireballDemo` | Mature menu framework/editor, nested color and map menus, map scaling/rotation, effects, cursor-size experiment. Main UI authoring dates July 2023; BirdProvider updates May 2024; scene/tracking edits July 2024. |
| October 31, 2024 | `Dropbox/Storage/Downloads_dir_20250402/BaseMenuElement.cs` | Same mature menu with a renamed debug flag and slower visual-test cycling; no substantive behavior expansion. |
| January 4, 2025 | MIT Unity `bird-mac` / `AssembleDevice` | AssemblySnap, ExtractSubmeshes and altered BirdInteractable; useful future manipulation/snapping reference. |
| 2024–March 2025 | Personal Dropbox Bird repo pair and AI Materials | Historical copies already represented in current Git ancestry, not another newer UI framework. |

File dates are approximate filesystem evidence, not proof of authored history.
Many directory timestamps cluster in September 2026 because of Dropbox
reconstruction/sync. Git dates above are separately verified commit evidence.

## What the mature UI actually does

`BaseMenuElement` has five states: Inactive, Enabled, Highlighted, Activated,
and Background. Collider actions support directional touch, point-at/leave,
point-through/leave, selection in bounds and selection beyond a target. Actions
can invoke UnityEvents, change state, summon children or close menus recursively.
Focus is associated with a user and shared between that user's hands. Visual
features store position/rotation/scale/color per state, animated with DOTween;
the custom editor captures and previews those states.

The recovered SampleScene (Unity 2020.3.33f1) contains 21 menu references:
9 direct components plus 12 FireButton prefab references.

- An upward passage through RootMenuTrigger opens MainFrame; downward passage closes it.
- Point-through highlights MainFrame controls. SelectBehind opens ColorBoopMenu or MapBoopMenu.
- ColorBoopMenu summons twelve FireButton instances laid out by DodecahedronGenerator. Their surrounding sphere drives RotateWithBird, allowing free spherical flick scrolling with momentum; Dana specifically endorses this feel. See [spherical-scroll behavior](SPHERICAL-SCROLL.md).
- MapBoopMenu exposes Bird-driven scaling and rotation.
- Serialized events include Close, StartScaling, StopScaling, ResetToInitialPositions and EnableLines.

Only SampleScene is enabled in build settings. This is a useful wired menu
reference, not evidence of the remembered multi-scene launcher.

## Preserved source and provenance

The light repository now contains:

- [2024 UI/source reference](../../Reference/RecoveredBirdUI2024/README.md):
  49 original source/meta/settings files, 106,955 bytes, including the October variant.
- [2025 assembly reference](../../Reference/RecoveredBirdAssembly2025/README.md):
  14 original source/meta/settings files, 42,334 bytes.

The heavy repository contains `Reference/RecoveredBirdUI2024`: the 2024 scene,
five relevant prefabs and their metas (12 files, 1,698,884 bytes). These are
reference snapshots outside active Unity projects. They are not complete runnable
projects: vendor SDKs, DOTween, models, materials and textures were not copied.
Original bytes and GUIDs are retained; manifests record source paths, timestamps,
lengths and SHA256 hashes. Git attributes disable text conversion for archived
data. Do not edit these snapshots as production code.

May BaseMenuElement SHA256:
`a317c7987754e055a3d355aeb9163274e515f54c37be87a88730cb0d40c7dfa4`.
October variant SHA256:
`526fc99cf6942c7496b6dcefa59044147e2cd4c9ede39a98ac914e40b46f28bf`.

The October variant renames `testVisualStates` to `cycleVisualStatesOnPlay` and
changes the test interval from 120 to 500 frames. The scene-connected May source
is the behavioral reference; the October file is preserved as a small revision.
The extensionless HighlightManager is an unfinished sketch, not active code.

## Recovered Fireball size law

`PointerDynamics.cs` applies:

```text
localScale = max(0.03, 3 * ln(1 + Bird hand-root range / 100))
```

It also faces the pointer relative to perspectiveOrigin. It contains no temporal
smoothing. The same formula appears in the older API copy in bird-mac. Its scale
floor ends around 1.005 m; values are about .118 at 4 m, .547 at 20 m, 2.079 at
100 m and 7.194 at 1000 m. These are local-scale values, not verified visible
world diameters; mesh and parent transforms matter. Current presentation uses
eye distance and Dana now explicitly requires constant size throughout the
broader working volume. Preserve that newer constraint when comparing curves.
This supersedes earlier notes that the original scaling script could not be found.

## Implementation direction: familiar behavior, conventional Unity design

Dana explicitly wants the interaction flavor, not the improvised development
style. Preserve directional touch, point-through navigation, focus, nested menu
behavior, visual states and action callbacks. Build a maintained implementation
with conventional serialized components/settings, Inspector workflows, prefabs,
UnityEvents where supported, isolated editor tooling and clear lifecycle rules.
Keep geometry, interaction state and optional presentation distinct. Avoid both
blindly importing the legacy class and inventing a sprawling custom framework.

Use the reference as executable-behavior guidance, with focused tests for:

- endpoint versus ray/point-through hit semantics and one transition per edge;
- both hands, user focus, tracking loss, disabling and destroyed targets;
- child opening, background actions, back/recursive close and same-frame callbacks;
- coherent hover/selection visuals, interrupted transitions and safe defaults;
- world-space menu use at near and far ranges without clamping Bird geometry;
- actual Unity runtime rendering plus a deliberate Udon-compatible action adapter.

Known legacy issues to resolve while implementing: SelectInBounds tests the
provider transform instead of the logical point; focus removal mutates an
enumerated list; action histories/close reentrancy need lifecycle review; ray
limits are hard-coded to 1000 m; some visual rotations are zero quaternions.
The recovered AssemblySnap disables BirdInteractable after snapping then returns
early when disabled, so its continuous unsnap idea needs reworking. Keep the
current core's newer safety/lifecycle fixes instead of replacing core scripts.

The first functional slice should reproduce open/highlight/select/back using
synthetic Bird input and then the live adapter. Bind Unity demo actions through
callbacks; the VRChat host must choose supported in-world navigation/actions,
not assume ordinary Unity scene loading is available. Follow with the nested
color/map behavior and the wider main-world plan, retaining mandala and paired
Hanoi ambitions. No recovered UI is active in the v0.8 Quest APK yet.

## Other copies and search coverage

Personal Dropbox `Personal Projects/Bird/bird-3d-cursor` is at `45389c4`
(2025-03-20), and `bird-3d-cursor-projects` at `f45a8b0` (2024-04-18). Both are
ancestors of maintained work. Checked core/Fireball files and AI Materials copies
match those histories. Two small July 2024 unitypackages in the Downloads archive
contain core/provider/tracking code, not menus.

The heavy maintained repo uses sparse checkout: some original Fireball scene and
script assets exist in Git but were not materialized on disk. This explains some
apparently missing files, but not the uncommitted mature menu family.

Search coverage included all 15 MIT Unity project roots; filename-first searches
of Desktop, relevant Documents/Downloads/source folders, personal Dropbox Bird,
VR Operating System/Sphere Computing/Storage/MAS Grad Program, MIT Shared, and
E: Cold Storage/Dropbox(Personal)/other/000 leads. Excluded Library/build caches,
software installs, virtual machines, unrelated private content and TimeMachine
backup internals. This is a broad targeted inventory, not a guarantee that every
archived copy on every volume was found.

Two existing recordings were identified but not opened/copied:
`C:/Users/Dana/Desktop/com.DanaGStudios.BirdUI-20210930-101526.mp4` and
`C:/Users/Dana/Desktop/com.DefaultCompany.BirdFireballDemo-20230615-230038.mp4`.
BirdSphereDemo's older scene may help later interaction-station design; the
maintained BirdInteractable already includes that code lineage and later fixes.
