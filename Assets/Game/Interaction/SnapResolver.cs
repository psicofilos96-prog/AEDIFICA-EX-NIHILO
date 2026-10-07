using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Interaction
{
    // Candidate search is isolated here so a spatial index can replace the lab scan.
    // Only the active manipulation calls Resolve; no per-piece Update or collider geometry.
    public sealed class SnapResolver
    {
        private readonly List<SnapFeature> moving = new List<SnapFeature>(26);
        private readonly List<SnapFeature> targets = new List<SnapFeature>(26);
        private bool hasLock;
        private Candidate locked;

        private struct Candidate
        {
            public GeometricSnapKind Kind;
            public PieceId TargetId;
            public int MovingIndex, TargetIndex;
            public Vector3 Correction, Point;
            public float Distance;

            public bool SameKey(Candidate other) => Kind == other.Kind && TargetId == other.TargetId &&
                MovingIndex == other.MovingIndex && TargetIndex == other.TargetIndex;
        }

        public bool HasTarget => hasLock;
        public GeometricSnapKind ActiveKind => locked.Kind;
        public Vector3 TargetPoint => locked.Point;
        public PieceId TargetId => locked.TargetId;

        public void Reset() => hasLock = false;

        public PieceData Resolve(PieceData raw, ManipulationSession session, SnapSettings settings,
            IEnumerable<PieceData> nearbyPieces)
        {
            if (raw == null || session == null || settings == null || nearbyPieces == null)
                throw new ArgumentNullException(nameof(raw));
            bool move = session.Mode == ManipulationMode.Move;
            bool face = session.Mode == ManipulationMode.Resize && session.ResizeBehavior == ResizeMode.Face;
            if (!settings.HasGeometricSnap || !move && !face) { Reset(); return raw; }
            settings.ValidateGeometric();
            moving.Clear();
            SnapGeometry.Collect(raw, moving, face, session.Axis, session.FaceSign);
            Vector3 outward = face ? raw.Transform.Rotation * ManipulationSession.AxisVector(session.Axis) * session.FaceSign : Vector3.zero;
            Candidate best = default;
            Candidate retained = default;
            bool foundBest = false, foundRetained = false;
            foreach (PieceData targetPiece in nearbyPieces)
            {
                if (targetPiece == null || targetPiece.Id == raw.Id) continue;
                targets.Clear();
                SnapGeometry.Collect(targetPiece, targets);
                foreach (SnapFeature source in moving)
                foreach (SnapFeature target in targets)
                {
                    if (source.Kind != target.Kind || !Enabled(source.Kind, settings) ||
                        !TryCorrection(source, target, out Vector3 correction, out Vector3 point)) continue;
                    bool sameLockedFeature = hasLock && locked.Kind == source.Kind && locked.TargetId == target.PieceId &&
                        locked.MovingIndex == source.Index && locked.TargetIndex == target.Index;
                    float geometricDistance = correction.magnitude;
                    if (face)
                    {
                        float along = Vector3.Dot(correction, outward);
                        if ((correction - outward * along).magnitude >
                            (sameLockedFeature ? settings.ReleaseDistance : settings.CaptureDistance)) continue;
                        float rawDimension = Dimension(raw, session.Axis);
                        if (rawDimension + along < raw.Dimensions.MinimumForAxis((int)session.Axis) - 0.00001f) continue;
                        correction = outward * along;
                    }
                    float distance = geometricDistance;
                    var candidate = new Candidate { Kind = source.Kind, TargetId = target.PieceId,
                        MovingIndex = source.Index, TargetIndex = target.Index,
                        Correction = correction, Point = point, Distance = distance };
                    if (hasLock && candidate.SameKey(locked) && distance <= settings.ReleaseDistance)
                    { retained = candidate; foundRetained = true; }
                    if (distance <= settings.CaptureDistance && (!foundBest || Better(candidate, best)))
                    { best = candidate; foundBest = true; }
                }
            }
            if (foundRetained) locked = retained;
            else if (foundBest) { locked = best; hasLock = true; }
            else { Reset(); return raw; }
            if (move)
                return raw.WithTransform(new PieceTransform(raw.Transform.Position + locked.Correction, raw.Transform.Rotation));
            float dimensionChange = Vector3.Dot(locked.Correction, outward);
            return session.ResizeToDimension(Dimension(raw, session.Axis) + dimensionChange);
        }

        private static bool Enabled(GeometricSnapKind kind, SnapSettings settings) => kind == GeometricSnapKind.Endpoint
            ? settings.EndpointSnapEnabled : kind == GeometricSnapKind.Edge ? settings.EdgeSnapEnabled : settings.SurfaceSnapEnabled;

        private static float Dimension(PieceData piece, ManipulationAxis axis) => axis == ManipulationAxis.X
            ? piece.Dimensions.X : axis == ManipulationAxis.Y ? piece.Dimensions.Y : piece.Dimensions.Z;

        private static bool Better(Candidate candidate, Candidate best)
        {
            if (candidate.Distance < best.Distance - 0.00001f) return true;
            if (candidate.Distance > best.Distance + 0.00001f) return false;
            if (candidate.Kind != best.Kind) return candidate.Kind > best.Kind;
            int idOrder = candidate.TargetId.CompareTo(best.TargetId);
            if (idOrder != 0) return idOrder < 0;
            if (candidate.MovingIndex != best.MovingIndex) return candidate.MovingIndex < best.MovingIndex;
            return candidate.TargetIndex < best.TargetIndex;
        }

        private static bool TryCorrection(SnapFeature moving, SnapFeature target, out Vector3 correction, out Vector3 point)
        {
            correction = point = Vector3.zero;
            if (moving.Kind == GeometricSnapKind.Endpoint)
            { point = target.A; correction = target.A - moving.A; return true; }
            if (moving.Kind == GeometricSnapKind.Surface)
            {
                if (Vector3.Dot(moving.Normal, target.Normal) > -0.999f) return false;
                if (target.Triangle)
                {
                    Vector3 triangleVertex = target.A - (target.U + target.V) / 3f;
                    point = ClosestPointOnTriangle(moving.A, triangleVertex,
                        triangleVertex + target.U, triangleVertex + target.V);
                    correction = point - moving.A;
                    return true;
                }
                Vector3 offset = moving.A - target.A;
                point = target.A + target.U * Mathf.Clamp(Vector3.Dot(offset, target.U) / target.U.sqrMagnitude, -1f, 1f)
                    + target.V * Mathf.Clamp(Vector3.Dot(offset, target.V) / target.V.sqrMagnitude, -1f, 1f);
                correction = point - moving.A;
                return true;
            }
            Vector3 a = moving.B - moving.A, b = target.B - target.A, r = moving.A - target.A;
            float aa = Vector3.Dot(a, a), bb = Vector3.Dot(b, b), ab = Vector3.Dot(a, b);
            float ar = Vector3.Dot(a, r), br = Vector3.Dot(b, r);
            float denominator = aa * bb - ab * ab;
            float s = denominator > 0.0000001f ? Mathf.Clamp01((ab * br - bb * ar) / denominator) : 0f;
            float t = Mathf.Clamp01((br + ab * s) / bb);
            s = Mathf.Clamp01((ab * t - ar) / aa);
            t = Mathf.Clamp01((br + ab * s) / bb);
            Vector3 movingPoint = moving.A + a * s;
            point = target.A + b * t;
            correction = point - movingPoint;
            return true;
        }

        // Closest point on an actual roof triangle, including edge and vertex regions.
        private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denominator = 1f / (va + vb + vc);
            return a + ab * (vb * denominator) + ac * (vc * denominator);
        }
    }
}
