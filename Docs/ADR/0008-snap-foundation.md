# ADR 0008: Snap foundation

## Decision

P0.7 adds optional transform assistance to the existing manipulation session. `SnapSettings` holds independent Position and Rotation toggles and increments (initial defaults: 0.5 m and 15°). G toggles Grid Snap and R toggles Angle Snap when no gizmo gesture is active. The Console reports each toggle once. There is no final settings UI. Both modes start disabled to preserve free manipulation until the player enables them.

`SnapPolicy` is pure math. Grid coordinates use the single world origin `(0,0,0)` and independently quantize the manipulated X, Y or Z coordinate. Angle Snap quantizes world Y yaw. Exact half increments round away from zero. Enabled increments must be finite and strictly positive; invalid settings cannot be enabled. Free mode returns the unsnapped candidate.

Each `ManipulationSession` captures the initial `PieceData` and a snapshot of its Snap settings. Every evaluation uses total pointer displacement from the gesture start, builds a transform candidate, then applies the policy before creating the replacement `PieceData`. The session never feeds a previously snapped transform back into the next evaluation. `ConstructionWorld` remains authoritative; `PieceView` follows its transform without mesh or material changes.

For an initially unaligned piece, a zero or small drag keeps the original transform. In the drag direction, the first world-grid line becomes active when the pointer's candidate crosses the midpoint between the starting coordinate and that first line. Subsequent candidates use the world grid. This prevents an immediate jump while converging to the world origin grid. A rotation starting between angle lines follows the same continuity rule. The untouched axes stay unchanged. Snapping is applied only during an active Move or Rotate gesture; disabling it never moves an idle piece.

Block, Wall and Slab use the same transform policy. Resize remains Center/Bilateral: the centre stays fixed and opposite faces move symmetrically. Future Face/Unilateral Resize must keep the opposite face fixed and change both dimension and position; it may consume Surface, Edge or Endpoint Snap. A future candidate-selection policy can add those sources after the raw transform candidate and before `PieceData` is committed. P0.7 creates no provider framework yet.

## Deferred

Surface, Edge, Endpoint, Midpoint and Vertex Snap; local or rotated grids; grid visuals; numeric entry; Resize Snap; Face/Unilateral Resize; and final settings UI are deferred.
