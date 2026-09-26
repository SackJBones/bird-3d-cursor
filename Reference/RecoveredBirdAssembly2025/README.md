# Recovered assembly experiment — reference only

Source/settings from local MIT Unity `bird-mac`, whose AssembleDevice experiment
was modified January 2025. Preserve as a reference for later shared Bird object
manipulation and paired Hanoi work. This is outside the active package, not a
complete runnable project, and not a replacement for maintained Bird core.

`AssemblySnap.cs` and `ExtractSubmeshes.cs` contain the new assembly experiment;
`BirdInteractable.cs` supplies its older modified interaction context. Note the
snap implementation disables the interactable then returns early on disabled
objects, so continuous unsnap needs redesign. `PointerDynamics.cs` repeats the
historical cursor size law; `ScaleByRange.cs` is a separate simpler experiment.

See [local history](../../docs/modernization/LOCAL-BIRD-HISTORY.md).
Original bytes and timestamps/hashes are recorded in `source-manifest.json`.
