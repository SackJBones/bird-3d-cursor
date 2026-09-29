# High-lookout rail finish

LookoutRail01 applies the pond rails' continuous square sweep to the one original
high-lookout perimeter. The 48 saved boundary segments supply the path and all
48 post positions. The top remains 0.13 m square, centered 1.05 m above the floor;
posts remain 0.065 m square and 1.06 m high. The square corners stay crisp while
normals along each face follow the bend. The loop closes without overlapping
end caps or cracks between independent boxes. This is a mesh finish, not a new
lookout layout or a change to Bird.

`Bird > Coastal world > Finish original lookout rail once` checks the original
segmented mesh before its first replacement. Do not replay this migration over
authored work. The existing mesh GUID and exact visible pointing collider remain.
The separate invisible player boundary and walking slab retain their bytes.
Only this renderer's saved lightmap scale becomes 4; a lighting rebake follows.
No new renderer, runtime component, material, light or network stream is added.

The ordinary saved mesh is editable. The explicit `Update lookout rail mesh from
saved boundary` command updates only its vertex/index/normal/UV channels while
retaining identity, transforms, material and Inspector lighting overrides. It
validates the original boundary layout before reading its ordered path. It does
not move the guard or resize the platform. Broader layout changes require a
separate deliberate authoring pass that checks all associated geometry.

`Invoke-UnityCoastalWorld.ps1 -CheckLookoutRail` checks closed oriented edges,
original dimensions, every post, clear spans and 192 top samples including the
old seams. Normal-frame compiled beacon tests retain the supported return stance
and add lookout guard pushes on both player layers. Before/after views cover the
return target, south/north loops, tight east end, closure, exterior and arrival.
Views 04 and 06 are free inspection cameras, not supported visitor stances.

The separate normal SDK Android inspection starts on the existing high-lookout
floor near the supported return stance, without a temporary platform. It restores
the source scene afterward. Return the dedicated Quest to normal production
arrival after any successful inspection. A tracking dialog or running process
is not actual world-rendering acceptance. See the latest checkpoint and heavy
`Reference/WorldBuildingReviews/20260929-LookoutRail01` for results, independent
review and physical-evidence limits. Raw device captures/logs remain private.
