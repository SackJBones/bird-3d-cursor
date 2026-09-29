# Readable beacon labels

Label01 fixes the doubled, mirrored words below the travel rings. The original
two world-space uGUI canvases used a two-sided material, so each viewpoint saw
both its intended text and the reverse of the opposite text. Their orientations
also faced inward relative to their small separation.

Each original canvas now faces outward and shares an ordinary saved material
using `Bird/Presentation/World Label`. This unlit world-space UI shader culls
back faces, respects scene depth and includes stereo, font texture, stencil and
rectangular clipping support. It is intended for world-space labels, not screen
overlay UI. See Unity's [Cull reference](https://docs.unity3d.com/2022.3/Documentation/Manual/SL-Cull.html).
The vertex path follows Unity's [custom stereo shader guidance](https://docs.unity3d.com/2022.3/Documentation/Manual/SinglePassInstancing.html)
and includes an instancing variant for the headset path.
The installed uGUI `Graphic` source and actual render checks establish the local
glyph orientation. No camera-following or custom runtime label script is required.

Near-white text and an ordinary uGUI Outline improve contrast over the cliff,
sea and white architecture. Existing destination names, preview instruction,
font, size, position and ring colors remain. Text naturally foreshortens near
edge-on; it is not enlarged at distance. Ring target geometry, landings, colliders
and interaction policy are unchanged. The saved world still offers targeting
preview: avatar clicks and teleport permission remain off pending physical tests.

The one-time `Bird > Coastal world > Fix original beacon labels once` command
updates only the ten label faces in the existing travel prefab and saves one
material. Do not replay it over authored assets. Thereafter edit the ordinary
Text, Outline, Canvas and shared material in the Inspector. Fresh beacon
authoring uses the same treatment. Accepted Bird fitting, range, filtering,
inflation, acquisition and complementary hand pairs are untouched.

`Invoke-UnityCoastalWorld.ps1 -CheckBeaconLabels` renders nine front, rear and
grazing viewpoints. It compares both faces against only the intended face,
requires the unintended face to have no visible pixels, and requires opaque
geometry to hide the label. Context views include the east approach, two sides,
oblique/edge-on views, high return and arrival. Views 02-05 are free diagnostic
cameras, not claimed supported visitor stances. Compiled beacon behavior checks
and normal SDK exports remain separate from physical headset comfort.

Label01's separate normal SDK Android inspection starts on the existing east
pond walk facing the water beacon. Later inspections may use a different stance;
consult the newest checkpoint. Source restoration is guarded; leave the Quest
on normal production arrival after inspection. See the latest checkpoint
and heavy `Reference/WorldBuildingReviews/20260929-Label01` for actual results,
independent critique and device evidence. Raw device data remains private.
