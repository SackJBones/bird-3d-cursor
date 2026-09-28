# Bird World architectural direction

Dana supplied a design conversation and four preferred images on 2026-09-27 in
the workspace's `world design` directory. All four images have been inspected and
the conversation read. Originals are preserved, byte-for-byte, in the heavy
repository at `Reference/WorldDesign20260927`, outside Unity Assets.

**Priority:** current VRChat Bird input and interaction work remains the most
important task. This brief guides presentation work when it becomes useful;
it does not introduce an architectural prerequisite to testing Bird.

**Authority:** Dana's latest instruction is to use the images for structure,
flow and architectural reference, with freedom to depart from their exact
appearance. Editability for iteration is paramount. The generated floor plan
was deliberately omitted because its result was unsatisfactory. Do not recover,
reconstruct or treat that drawing as an approved layout. The accompanying
written discussion remains useful guidance; dimensions and final connections
are still open to iteration.

## Experience and sequence

Visitors spawn within a broad, smooth, white, cavernous interior built into a
steep coastal mountain ridge. There is a plain wall behind spawn; a physically
plausible tunnel entrance is unnecessary. Flat standing areas and generous
empty surfaces make the space legible and comfortable. Rocks should not appear
inside this arrival room.

The Bird pickup/first-try area is the architectural centerpiece, framed by a
prominent, clean circular opening. Preserve the circle and stairs as spatial
ideas; the discussion places the stairs close to the opening. Make the route
through the bottom of the circle clearly traversable. The first view outside
is largely a substantial support column with sweeping architecture around it,
with only a small glimpse of natural coast. Reveal the larger vista later.
The pictured bird sculpture/pedestal is conceptual reference, not a requirement
to make literal bird artwork the interaction UI.

The additional circular-opening/steps reference emphasizes the clean full-circle
threshold, shallow nearby stairs, a substantial branching white support and
sweeping inhabited structure immediately beyond it. Use this alongside the
arrival cavern image when composing the first view and traversable route.
Dana's later filename clarification says the view need not be this open and can
show more architecture; the image also supplies reference for the distant island.
Retain the earlier largely architectural first reveal rather than treating the
picture's broad exposure to sky and sea as mandatory.
The additional sculptures reference suggests quiet display spaces with warm
floors, generous separation and pale, folded organic forms on simple plinths.
It is reference for sculptural form and a comfortable gallery atmosphere;
specific objects and placements remain editable design choices.

At arrival, visitors can choose a cozy wing to either side or proceed outward.
The outdoor complex offers additional intimate spaces, including an inviting
visible room and a more tucked-away discovery. Give each a simple distinctive
feature. These spaces should feel pleasant to inhabit independently of demos;
the notes cite the Kitsunebi teahouse as a reference for that social arrangement.

Outside, include a conversation pit, a lower-level fish pool or moat-like water
edge near an overlook, and sparse elevated lookout floors. Water is a supporting
feature, not the whole composition. Across the water, a significant but bounded
steep-sided island offers a distant interaction setting without a planned
visitor route. Preserve long sightlines and large-scale features that make
Bird's range meaningful.

## Form and atmosphere

Dana's latest material direction (2026-09-27): the architecture should read as
full, bright white, basically matte, with at most a tiny sheen if inexpensive
on Quest. Prefer neutral white over ivory, cream or beige. Preserve enough
lighting and shading to read the curved forms; do not interpret this as a
requirement for emissive/unlit surfaces or costly real-time reflections.
Judge the rendered result as well as the material swatch, including interiors.

The site is a long, slightly curving coastal ridge, not an isolated mountain
peak. The white complex is the distinctive curved architectural presence;
avoid repeating the same architectural language in unrelated distant buildings.

The conversation developed from slope-following terraces toward **vertical,
stacked, inhabited forms** on a steep site: broad overhanging ceilings,
substantial foreground supports, quiet central masses and useful interior
floors. Structural plausibility matters. References to Greg Lynn and Zaha Hadid
describe organic massing and sweeping supports; they do not demand a specific
building replica, honeycomb grids, runtime ray marching or high polygon counts.

Warm cozy openings sit against the cliff side; much of the central architecture
remains calm white mass or simple lookout levels. Organic forms contrast with
clear flat walking surfaces, stairs and readable height changes. Plain materials,
low-detail surroundings and areas of visual quiet are intentional. Favor broad
silhouettes and proportions over decorative density or dramatic rendering.

## Implementation approach for later work

The following is an implementation proposal, not a fixed floor plan:

- Begin with an editable blockout of arrival, circular threshold, major column,
  social wings, outdoor terraces, overlook/water edge and distant island.
  Evaluate the visitor's sequence from standing viewpoints before adding detail.
- Keep spaces, circulation, demo stations, terrain and presentation separate in
  the hierarchy. Use ordinary prefabs, serialized parameters and authored anchors
  so one room or route can move without rebuilding the entire world.
- Expose curve profiles, control points, spans, heights, overhangs and silhouette
  detail in editor-time mesh tools where useful. Save normal meshes/prefabs for
  the runtime world. Regeneration should be explicit, scoped to owned geometry
  and preserve authored overrides and unrelated work.
- Keep walkable/collision geometry simple and independently adjustable. Treat
  visual curves and interaction workspaces as separate authoring concerns.
  Verify reachability, stairs, head clearance, spawn safety and useful sightlines
  in the blockout, before investing in artwork.
- Reserve replaceable demo anchors for nearby/far Hanoi, the color selector,
  mandala and future interactions. Their exact placement is not specified by
  either image. Neither UI cursor artwork nor architecture belongs in Bird's
  geometric solver.
- Budget mesh detail, materials, transparency, water and distant content for
  Quest, and measure actual rendering cost. The reference pictures are not a
  performance guarantee. Preserve enormous logical Bird reach; use deliberate
  visual detail/visibility choices without adding a short interaction range cap.

The existing tracking lab and standalone high-overlook vista remain test
harnesses. Neither is the final Bird World layout. This brief adds direction
for that world without replacing their current testing role.

## Independent critique across world-building cycles

Dana explicitly requests an independent-review-critic-agent pattern when world
building begins. Give an independent critic the complete concept conversation,
all four images, this brief and current scene evidence. The critic should judge
the implementation against Dana's expressed intent rather than literal image
matching or the implementer's description of success. Keep development and
critique separate, revise from concrete findings, and repeat across cycles.

Use a documented 0-10 scale for **aesthetics, navigability, hangout suitability,
and overall VRChat world quality**. As an initial operational target (not a
user-specified threshold), aim for at least 8/10 in every category with no
unresolved major navigation or usability issue. Each score needs evidence,
remaining weaknesses and prioritized actionable changes. Do not game a score
by relaxing the rubric or discarding inconvenient observations. Preserve review
history and hand off unresolved findings to the next cycle.

Assess arrival/threshold reveal, supported vertical coastal massing, visual
quiet and coherent silhouettes for aesthetics; routes, stairs, clearances,
wayfinding and access to social/demo spaces for navigation; human-scale cozy
groups, conversation arrangements, discovery and comfortable views for hanging
out; and spawn behavior, interaction clarity, scale, collisions, Quest rendering
cost and everyday VRChat usability for overall quality. Review editable scene
structure as well as screenshots. Later reviews should include walkthrough and
runtime evidence, not only flattering fixed-camera images. State what cannot be
judged without in-headset or multiplayer observation; numerical ratings are
review judgments, not substitutes for those checks or Dana's feedback.

## Asset sourcing and credits

Dana authorizes fetching useful free assets and packages, with correct credit.
Check the actual license and its compatibility with the intended Unity/VRChat
use and any source-repository redistribution before adopting an asset; free
price alone is not a redistribution license. Record source URL, author,
version/download date, license text or stable license reference, modifications
and required attribution. Preserve notices with the asset and maintain the
world's credits, including in-world attribution when required. If a license
allows use but not source redistribution, keep the asset out of public Git and
record a reproducible acquisition step. Prefer maintainable, editable assets
that suit the Quest budget and design direction; availability alone is not a
reason to add a dependency.

## References

- [Preserved conversation and provenance](../../../bird-3d-cursor-projects/Reference/WorldDesign20260927/README.md)
- [Exterior complex](../../../bird-3d-cursor-projects/Reference/WorldDesign20260927/complex%20lowpoly.png)
- [Arrival cavern](../../../bird-3d-cursor-projects/Reference/WorldDesign20260927/spawn%20point%20cavern%20lowpoly.png)
- [Circular opening and steps](../../../bird-3d-cursor-projects/Reference/WorldDesign20260927/circular%20opening%20and%20steps%20reference%20lowpoly.png)
- [Sculptures and gallery atmosphere](../../../bird-3d-cursor-projects/Reference/WorldDesign20260927/sculptures%20lowpoly.png)
