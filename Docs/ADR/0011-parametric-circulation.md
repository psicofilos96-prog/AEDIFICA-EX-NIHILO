# ADR 0011 — Parametric stairs and ramps (P0.10)

Stair and Ramp are independent `PieceData` families. Their base is `PieceTransform.Position`; local `-Z` is the low end, local `+Z` the high end, and yaw comes only from the existing transform. Stair local Y=0 is the solid underside. Ramp local Y=0 is the lowest underside; its top is `Thickness` above the sloping underside. Thus its total bounds height is `Height + Thickness`, while `Height` remains the authoritative rise. Pitch, riser height and tread depth are derived, never stored.

One `CirculationProfile` supplies the sections used by mesh generation and geometric snap. Stair uses a solid stepped profile with N treads and no internal step walls; Ramp uses a closed sloping prism. Each piece has one renderer, one generated mesh and one `MeshCollider`, created explicitly before refresh. Geometry and collider are replaced only when semantic dimensions change. Material, pose and selection changes retain the mesh. `localScale` is one.

The Stair snap set deliberately contains the architectural low and high ends, underside, first and last tread, outer edges and representative side surfaces. Feature count stays bounded as `StepCount` grows. The full stair mesh still grows linearly. `StepCount` is limited to 1..128 to bound mesh rebuilding; no ergonomic stair rule is imposed. Ramp exposes its end edges and faces, sloping top and bottom, and sides. Snap uses the existing capture/release settings and resolver.

Resize X edits Width, Z edits Run, and Y edits Stair Height or Ramp Height. Ramp thickness stays fixed during Y resize, so the gizmo edits total bounds height with a minimum of `Thickness + 0.1 m`. Face Resize retains the opposite world face. Ramp Thickness is set at creation in this phase. The temporary `[` and `]` keys decrement/increment selected Stair StepCount by one; they create replacement `PieceData` and update view/collider. There is no settings UI or automatic connection between structures.

Coplane overlaps of independent pieces may still z-fight, as accepted before P0.10.
