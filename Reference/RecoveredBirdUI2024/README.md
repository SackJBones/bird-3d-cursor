# Recovered Bird UI — reference only

Original custom source from Dana's local 2023–2024 Fireball work, recovered
2026-09-26. This is outside the active Unity package and is not a runnable sample.
Use it to recover interaction intent; rebuild production components using normal
Unity conventions. Do not copy its older core assumptions or lifecycle defects.

- `fireball-may-july2024/Assets/Scripts/BirdMenuScripts/BaseMenuElement.cs`: mature menu behavior.
- `fireball-may-july2024/Assets/Editor/BaseMenuElementEditor.cs`: Inspector state capture/preview.
- `fireball-may-july2024/Assets/Scripts/PointerDynamics.cs`: original logarithmic cursor-size experiment.
- Other custom scripts preserve color/map/effect/manipulation context.
- `october2024-variant/BaseMenuElement.cs`: later debug-only revision.

[Full inventory and implementation direction](../../docs/modernization/LOCAL-BIRD-HISTORY.md).
Scene/prefab snapshots live in the heavy repository under the matching
`Reference/RecoveredBirdUI2024` path. DOTween/vendor assets were not copied.
`source-manifest.json` records original source paths, byte lengths, SHA256 and
filesystem timestamps. `.gitattributes` preserves original bytes. Keep originals
unchanged; make production improvements in the maintained package/integration.
