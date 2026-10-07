# ADR 0010: Parametric roofs

P0.9 adds FlatRoof, ShedRoof, GableRoof and HipRoof as semantic `PieceType` values. `PieceData` remains authoritative. Roof parameters are Width (local X), Depth (local Z), Thickness, and for sloped roofs Rise. Rise is the only stored slope parameter; pitch is `atan(Rise / horizontalRun)` and is never stored. `PieceDimensions.Y` is the derived total height, while Thickness and Rise are retained independently to avoid subtractive drift.

Local Y=0 is the lowest underside eave. The top surface has vertical thickness above an identical underside profile. Shed rises from local -Z to +Z. Gable has a central ridge parallel to local X. Hip uses a centered ridge along the longer footprint axis, with ridge length `abs(Width - Depth)`; equal dimensions produce one central apex. All are deterministic and independent of walls or other pieces.

FlatRoof uses the existing prism mesh and box collider. Shed/Gable/Hip use a small procedural closed mesh and one nonconvex MeshCollider on their static PieceView, updated only when geometric parameters change. The required BoxCollider component is disabled for those roofs. Material changes retain the mesh and collider. No Rigidbody is used.

Move and yaw rotation use the existing tools. FlatRoof can resize X/Y/Z, with Y representing Thickness. Sloped roofs expose only X (Width) and Z (Depth) resize handles. Bilateral and unilateral X/Z resize retain the opposite footprint edge and preserve Thickness/Rise. A Y handle would ambiguously modify Thickness, Rise, or both, so it is hidden. In ConstructionLab, PageUp/PageDown changes the selected sloped roof's Rise by 0.1 m per press, clamped to 0.1 m, through PieceData; the Console reports the value. This is temporary laboratory input, not a settings UI.

SnapGeometry obtains eaves, corners, ridge, roof planes, and fascia from the same RoofProfile used by mesh generation. It does not use a world AABB for sloped roof features. Surface snap against a triangular roof plane computes the closest point on that triangle. The current snap resolver still performs geometric translation only; it does not match pitch or join roofs automatically.
