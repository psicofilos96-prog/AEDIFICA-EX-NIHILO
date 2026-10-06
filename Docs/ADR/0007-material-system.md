# ADR 0007: Material system foundation

## Decision

`PieceData` stores `MaterialId` alongside identity, type, transform and dimensions. `MaterialId` is a normalized, nonempty GUID value like `PieceId`; it is stable across renames and suitable for future Save/Load and blueprints. Existing piece constructors default to the stable Neutral ID. `WithTransform` and `WithDimensions` preserve it, while `WithMaterial` changes only the ID. An unknown or absent ID remains representable so old or incomplete data can still render.

`ConstructionLabBlocks` owns one `MaterialRegistry` for the scene. Its serialized Unity material references are registered once, and all `PieceView`s share that registry. `TryGet` resolves known IDs; `Resolve` returns the shared Neutral asset for unknown or absent IDs. Missing IDs can be logged once per registry when scene diagnostics are enabled. No renderer owns a new material instance. The registry's small ordered list also supports the temporary `M` key: with a piece selected, cycle to the next available lab material through `ConstructionWorld.Apply`.

`PieceView.Refresh` compares dimensions and material identity separately. A changed `MaterialId` updates `MeshRenderer.sharedMaterial`; only changed dimensions rebuild the cuboid mesh and collider. Selection highlight uses `MaterialPropertyBlock` and does not replace the shared material. Geometry is independent of material identity.

## Lab assets

Neutral, Stone, Brick and Plaster are four simple shared URP Lit placeholders. The existing Blocks use Neutral, the two Walls use Stone and Brick, and Slab uses Plaster. They establish stable IDs and demonstrate distinct materials; they are not final historical art.

## Deferred

A final material UI, thumbnails, texture library, shaders, persistence and procedural variation are deferred. Future construction UX needs two Resize modes: Center/Bilateral and Face/Unilateral. P0.6 does not change resize behaviour.
