# Parametric architectural finishes — first functional increment

Base: `pvt/free-assets-visual-reference` at `49512a86d4cfaf504047bd84617226f93395b51b`. This increment adds dependent architectural finish **data**, not a building prefab or an autonomous construction system. The player chooses the host wall/roof and finish. The engine only derives the selected finish's mesh from its host.

## Data contract and ownership

`ArchitecturalFinish` contains a stable `Guid Id`, `PieceId HostId`, optional `Guid OpeningId`, kind, material ID and bounded profile/projection dimensions. It contains no world position, transform, GameObject, mesh or pointer to another mutable object. A host `PieceData` owns an immutable read-only list of finishes. Opening finishes reference an opening of that same host wall; eaves reference a roof and use `Guid.Empty` for the opening. Duplicate finish IDs or two finishes of the same kind on the same opening are rejected. Door frames require passages; window frames with sill require windows; lintels accept either. No reference to another finish is possible, so cycles cannot be created.

The four initial kinds are `DoorFrame`, `WindowFrameSill`, `Lintel`, `RoofEave`. Finishes use existing shared stone/wood URP materials. `ArchitecturalFinishGeometry` computes local-space jambs, head, sill, lintel or four roof-edge sections from the current host dimensions and opening rectangle. The window-frame kind also derives a thin glazed panel using the already registered shared glass material; it is not a separate orphan PieceData. UVs are generated in metres from each block surface; normals transform with the roof-edge rotation. The host transform is applied only at rendering. There are no stored absolute trim coordinates.

A finish is **dependent geometry owned by its host**, not an independent `PieceData`; its persistent Guid supplies identity for editing/removal and future UI. Structural pieces and openings remain the construction authority. Free CC0 rocks/vegetation/pots remain environmental, outside `ConstructionWorld`. The residence's existing timber door leaf is an independently authored logical piece: it does not currently follow a moved wall. This separation is intentional but leaves a doorway coordination task for future editing UX; it is not presented as a solved dependent finish.

## Lifecycle and localized rendering

`PieceData.WithFinish` adds a validated finish. `WithTransform`, `WithDimensions`, `ReplaceOpening` and `WithMaterial` retain the finish IDs. Geometry is recalculated for changed dimensions and opening rectangles. `WithoutOpening` removes only finishes referencing that opening. Deleting a host removes all dependent finish data because it is inside the host snapshot. A rejected invalid wall resize never mutates the world. `ConstructionWorld.SameState` compares finish data so finish-only edits produce a real ChangeSet. `ConstructionCommandHistory` records one host snapshot; undo/redo restores host and finishes atomically without a second command stream.

`PvtChunkVisualEngine` adds finish segments to material-grouped visual pages, mapping their triangles back to the host PieceId for selection. On host change its existing dirty-region mechanism rebuilds the affected visual region. `PieceView` displays the same derived finish segments when the host is extracted for editing. Spatial bounds gain a conservative 0.6 m margin per axis on hosts with finishes, covering the maximum permitted profile and projection near chunk boundaries. This can index a finish-bearing host in adjacent chunks; no large-scale benchmark was run.

## Persistence and compatibility

`Pvt2Snapshot` writes format version 2 with a finish count and records after each piece's openings. It reads both v1 and v2; a v1 piece receives an empty finish collection. Unsupported versions remain rejected. Save/load preserves host, opening and finish IDs. The existing v1 file layout was not rewritten. The loaded data is validated through the same `PieceData` barriers, so a dangling host/opening reference is rejected instead of silently repaired.

## Windows Unity validation

1. In an isolated checkout, run `git fetch origin pvt/parametric-architectural-finishes`, then `git switch --track origin/pvt/parametric-architectural-finishes` if no local branch exists. Keep any working changes and stashes intact. Open this checkout in Unity 6000.6.4f1.
2. Run all EditMode and PlayMode tests. In particular inspect `ArchitecturalFinishTests`, `ArchitecturalFinishPlayModeTests` and the existing free-reference tests. Record counts and any Console errors. The remote environment has no Unity Editor; no Unity test pass is asserted here.
3. If the free-reference scene has not been generated in this checkout, use **Tools → Aedifica → Free Asset Reference → Prepare CC0 Assets** then **Create Reference Scene**. Open `Assets/Game/Scenes/PvtFreeAssetsReference.unity` and enter Play Mode.
4. Review the `finished facade` and `door/window/eave detail` camera buttons in Game View at 1920×1080. Existing wall openings and roof eave should already have linked finishes. Select a wall and use Move +X; its frame/lintel must move with it. Use Undo/Redo, Resize +X and Opening +0.1m. The selected host must keep its finish IDs. Choose a wall or roof and use the Add/Remove finish buttons. Unsupported choices must report a clear message. Test Delete, Undo, Save and Load, then inspect again.
5. Use **Capture 4 views** for PNGs in `Application.persistentDataPath`. Compare the two finish views before/after operations, and inspect seams, texture scale and selected-piece highlight. A Windows Player visual check remains necessary before art approval.

## Limits and gate

This is a small four-kind geometry system. It does not create doors/windows or redesign architecture. The old full-city visual recipe retains its previous independent trim pieces; only `BuildReferenceResidence()` switches to dependent opening frames and roof eaves. The free-reference house still has separate door leaf/stiles; removing/moving a wall alone can leave those independent items behind. The linked trim meshes use simple extruded sections with existing PBR surfaces; human review must decide whether they are visually convincing. No FPS, VRAM or city-scale result is claimed. Further visual refinement should follow actual Game View captures, not synthetic renders.
