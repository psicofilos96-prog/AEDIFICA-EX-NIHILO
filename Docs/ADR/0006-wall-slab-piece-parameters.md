# ADR 0006: Wall and Slab piece parameters

## Context

P0.2 stored `BlockDimensions` directly in `PieceData`, which was sufficient for Block but would make Wall and Slab either masquerade as Blocks or require a growing collection of optional dimension fields. Both approaches permit invalid type/parameter combinations.

## Decision

`PieceData` retains independent `PieceId`, `PieceType`, and `PieceTransform`, and stores one `PieceDimensions` value. This readonly tagged value contains three finite, positive dimensions and the matching `PieceType`. Constructors accept semantic values: `BlockDimensions`, `WallDimensions`, or `SlabDimensions`. Accessors reject a mismatched family; replacement preserves the original type. No reflection, boxing, per-piece behaviour script, or GameObject state is needed to interpret dimensions.

The axes are local X/Y/Z. Block means Width/Height/Depth; Wall means Length/Height/Thickness; Slab means Width/Thickness/Depth. The pivot is the centre of the base for all three. `PieceTransform.Rotation` orients each piece in world space. Resize changes the semantic parameter for the chosen axis, with a 0.1 m minimum, and leaves `PieceTransform` unchanged.

The three types remain distinct in the model even though P0.5 renders all of them with the same 24-vertex cuboid topology. `BlockGeometryGenerator.GeneratePiece` reuses its flat normals, metre-scaled UVs, bounds and base pivot. `PieceView` derives mesh and collider from the tagged dimensions and keeps local scale at one. `ConstructionWorld` remains authoritative.

Future piece families can add a validated semantic parameter type and a tagged value conversion. A future shape with non-cuboid topology will choose geometry by `PieceType`; the model need not gain optional fields for every family.

## Deferred

Wall drawing, snapping, openings, CSG, automatic joins, materials, creation UI, terrain, save/load and advanced building parts are outside P0.5.
