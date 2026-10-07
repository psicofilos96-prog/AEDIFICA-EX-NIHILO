using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Interaction
{
    public enum GeometricSnapKind { Surface, Edge, Endpoint }

    // Transient features derived from PieceData. A/B are endpoints for edges;
    // Rectangular faces use A as center and U/V as half-extents. For roof
    // triangles, A is the centroid and U/V are full edges from vertex zero.
    public readonly struct SnapFeature
    {
        public readonly GeometricSnapKind Kind;
        public readonly PieceId PieceId;
        public readonly int Index;
        public readonly Vector3 A, B, Normal, U, V;
        public readonly bool Triangle;

        public SnapFeature(GeometricSnapKind kind, PieceId pieceId, int index,
            Vector3 a, Vector3 b, Vector3 normal, Vector3 u, Vector3 v, bool triangle = false)
        {
            Kind = kind; PieceId = pieceId; Index = index;
            A = a; B = b; Normal = normal; U = u; V = v;
            Triangle = triangle;
        }
    }

    public static class SnapGeometry
    {
        // Corner index bits: X=1, Y=2, Z=4. Y=0 is the piece base.
        private static Vector3 LocalCorner(PieceData piece, int index) => new Vector3(
            (index & 1) == 0 ? -piece.Dimensions.X * 0.5f : piece.Dimensions.X * 0.5f,
            (index & 2) == 0 ? 0f : piece.Dimensions.Y,
            (index & 4) == 0 ? -piece.Dimensions.Z * 0.5f : piece.Dimensions.Z * 0.5f);

        private static bool OnMovingFace(int corner, ManipulationAxis axis, int sign)
        {
            int bit = axis == ManipulationAxis.X ? 1 : axis == ManipulationAxis.Y ? 2 : 4;
            return ((corner & bit) != 0) == (sign > 0);
        }

        public static void Collect(PieceData piece, List<SnapFeature> output,
            bool onlyFace = false, ManipulationAxis movingAxis = ManipulationAxis.X, int faceSign = 1)
        {
            if (piece.Dimensions.IsSlopedRoof)
            {
                CollectRoof(piece, output, onlyFace, movingAxis, faceSign);
                return;
            }
            if (piece.Dimensions.IsStair || piece.Dimensions.IsRamp)
            {
                CollectCirculation(piece, output, onlyFace, movingAxis, faceSign);
                return;
            }
            Vector3[] corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                corners[i] = piece.Transform.Position + piece.Transform.Rotation * LocalCorner(piece, i);
                if (!onlyFace || OnMovingFace(i, movingAxis, faceSign))
                    output.Add(new SnapFeature(GeometricSnapKind.Endpoint, piece.Id, i,
                        corners[i], corners[i], Vector3.zero, Vector3.zero, Vector3.zero));
            }
            int edgeIndex = 0;
            foreach (int bit in new[] { 1, 2, 4 })
                for (int i = 0; i < 8; i++)
                    if ((i & bit) == 0)
                    {
                        int other = i | bit;
                        if (!onlyFace || OnMovingFace(i, movingAxis, faceSign) && OnMovingFace(other, movingAxis, faceSign))
                            output.Add(new SnapFeature(GeometricSnapKind.Edge, piece.Id, edgeIndex,
                                corners[i], corners[other], Vector3.zero, Vector3.zero, Vector3.zero));
                        edgeIndex++;
                    }
            Vector3 center = piece.Transform.Position + piece.Transform.Rotation * new Vector3(0f, piece.Dimensions.Y * 0.5f, 0f);
            for (int axis = 0; axis < 3; axis++)
            for (int side = -1; side <= 1; side += 2)
            {
                if (onlyFace && (axis != (int)movingAxis || side != faceSign)) continue;
                Vector3 localAxis = ManipulationSession.AxisVector((ManipulationAxis)axis);
                float extent = axis == 0 ? piece.Dimensions.X : axis == 1 ? piece.Dimensions.Y : piece.Dimensions.Z;
                Vector3 normal = piece.Transform.Rotation * localAxis * side;
                Vector3 u = piece.Transform.Rotation * (axis == 0 ? Vector3.up * piece.Dimensions.Y * 0.5f
                    : Vector3.right * piece.Dimensions.X * 0.5f);
                Vector3 v = piece.Transform.Rotation * (axis == 2 ? Vector3.up * piece.Dimensions.Y * 0.5f
                    : Vector3.forward * piece.Dimensions.Z * 0.5f);
                Vector3 faceCenter = center + normal * (extent * 0.5f);
                output.Add(new SnapFeature(GeometricSnapKind.Surface, piece.Id, axis * 2 + (side > 0 ? 1 : 0),
                    faceCenter, faceCenter, normal, u, v));
            }
        }

        private static void CollectCirculation(PieceData piece, List<SnapFeature> output,
            bool onlyFace, ManipulationAxis axis, int sign)
        {
            CirculationProfile profile = CirculationProfile.Create(piece.Dimensions);
            CirculationSection first = profile.Sections[0];
            CirculationSection last = profile.Sections[profile.Sections.Length - 1];
            float x = piece.Dimensions.X * 0.5f;
            int pointIndex = 0, edgeIndex = 0, surfaceIndex = 0;
            foreach (float side in new[] { -x, x })
            {
                Vector3 lowBottom = new Vector3(side, first.BottomStart, first.StartZ);
                Vector3 highBottom = new Vector3(side, last.BottomEnd, last.EndZ);
                Vector3 lowTop = new Vector3(side, first.TopStart, first.StartZ);
                Vector3 highTop = new Vector3(side, last.TopEnd, last.EndZ);
                AddCirculationPoint(piece, output, lowBottom, ref pointIndex, onlyFace, axis, sign);
                AddCirculationPoint(piece, output, highBottom, ref pointIndex, onlyFace, axis, sign);
                AddCirculationPoint(piece, output, lowTop, ref pointIndex, onlyFace, axis, sign);
                AddCirculationPoint(piece, output, highTop, ref pointIndex, onlyFace, axis, sign);
                AddCirculationEdge(piece, output, lowBottom, highBottom, ref edgeIndex, onlyFace, axis, sign);
                AddCirculationEdge(piece, output, lowTop, new Vector3(side, first.TopEnd, first.EndZ),
                    ref edgeIndex, onlyFace, axis, sign);
                AddCirculationEdge(piece, output, new Vector3(side, last.TopStart, last.StartZ), highTop,
                    ref edgeIndex, onlyFace, axis, sign);
                AddCirculationEdge(piece, output, lowBottom, lowTop, ref edgeIndex, onlyFace, axis, sign);
                AddCirculationEdge(piece, output, highBottom, highTop, ref edgeIndex, onlyFace, axis, sign);
            }
            foreach (float side in new[] { first.StartZ, last.EndZ })
            {
                CirculationSection section = side == first.StartZ ? first : last;
                float bottom = side == first.StartZ ? section.BottomStart : section.BottomEnd;
                float top = side == first.StartZ ? section.TopStart : section.TopEnd;
                AddCirculationEdge(piece, output, new Vector3(-x, bottom, side), new Vector3(x, bottom, side),
                    ref edgeIndex, onlyFace, axis, sign);
                AddCirculationEdge(piece, output, new Vector3(-x, top, side), new Vector3(x, top, side),
                    ref edgeIndex, onlyFace, axis, sign);
                Vector3 normal = side == first.StartZ ? Vector3.back : Vector3.forward;
                AddCirculationSurface(piece, output, new Vector3(0f, (bottom + top) * 0.5f, side),
                    normal, Vector3.right * x, Vector3.up * ((top - bottom) * 0.5f),
                    ref surfaceIndex, onlyFace, axis, sign);
            }
            // Only first/last treads are snap features. The mesh still contains every step.
            for (int selected = 0; selected < (profile.IsStair && profile.Sections.Length > 1 ? 2 : 1); selected++)
            {
                CirculationSection section = selected == 0 ? first : last;
                Vector3 topCenter = new Vector3(0f, (section.TopStart + section.TopEnd) * 0.5f,
                    (section.StartZ + section.EndZ) * 0.5f);
                Vector3 along = new Vector3(0f, (section.TopEnd - section.TopStart) * 0.5f,
                    (section.EndZ - section.StartZ) * 0.5f);
                Vector3 topNormal = Vector3.Cross(along, Vector3.right).normalized;
                AddCirculationSurface(piece, output, topCenter, topNormal, Vector3.right * x, along,
                    ref surfaceIndex, onlyFace, axis, sign);
                if (profile.IsStair)
                {
                    foreach (float side in new[] { -x, x })
                    {
                        AddCirculationPoint(piece, output, new Vector3(side, section.TopStart, section.StartZ),
                            ref pointIndex, onlyFace, axis, sign);
                        AddCirculationPoint(piece, output, new Vector3(side, section.TopEnd, section.EndZ),
                            ref pointIndex, onlyFace, axis, sign);
                        AddCirculationSurface(piece, output,
                            new Vector3(side, section.TopStart * 0.5f, (section.StartZ + section.EndZ) * 0.5f),
                            side < 0f ? Vector3.left : Vector3.right,
                            Vector3.forward * ((section.EndZ - section.StartZ) * 0.5f),
                            Vector3.up * (section.TopStart * 0.5f),
                            ref surfaceIndex, onlyFace, axis, sign);
                    }
                }
            }
            if (profile.IsRamp)
            {
                Vector3 bottomCenter = new Vector3(0f, (first.BottomStart + first.BottomEnd) * 0.5f,
                    (first.StartZ + first.EndZ) * 0.5f);
                Vector3 bottomAlong = new Vector3(0f, (first.BottomEnd - first.BottomStart) * 0.5f,
                    (first.EndZ - first.StartZ) * 0.5f);
                AddCirculationSurface(piece, output, bottomCenter,
                    Vector3.Cross(Vector3.right, bottomAlong).normalized, Vector3.right * x, bottomAlong,
                    ref surfaceIndex, onlyFace, axis, sign);
                foreach (float side in new[] { -x, x })
                {
                    Vector3 a = new Vector3(side, first.BottomStart, first.StartZ);
                    Vector3 b = new Vector3(side, first.BottomEnd, first.EndZ);
                    Vector3 c = new Vector3(side, first.TopEnd, first.EndZ);
                    Vector3 d = new Vector3(side, first.TopStart, first.StartZ);
                    AddCirculationTriangle(piece, output, a, b, c, side < 0f ? Vector3.left : Vector3.right,
                        ref surfaceIndex, onlyFace, axis, sign);
                    AddCirculationTriangle(piece, output, a, c, d, side < 0f ? Vector3.left : Vector3.right,
                        ref surfaceIndex, onlyFace, axis, sign);
                }
            }
        }

        private static bool OnCirculationFace(PieceData piece, Vector3 point, ManipulationAxis axis, int sign)
        {
            float extent = axis == ManipulationAxis.X ? piece.Dimensions.X * 0.5f
                : axis == ManipulationAxis.Y ? piece.Dimensions.Y : piece.Dimensions.Z * 0.5f;
            float coordinate = axis == ManipulationAxis.X ? point.x : axis == ManipulationAxis.Y ? point.y : point.z;
            return Mathf.Abs(coordinate - (axis == ManipulationAxis.Y && sign < 0 ? 0f : sign * extent)) < 0.0001f;
        }

        private static void AddCirculationPoint(PieceData piece, List<SnapFeature> output, Vector3 local,
            ref int index, bool onlyFace, ManipulationAxis axis, int sign)
        {
            if (!onlyFace || OnCirculationFace(piece, local, axis, sign))
                output.Add(new SnapFeature(GeometricSnapKind.Endpoint, piece.Id, index,
                    World(piece, local), Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero));
            index++;
        }

        private static void AddCirculationEdge(PieceData piece, List<SnapFeature> output, Vector3 a, Vector3 b,
            ref int index, bool onlyFace, ManipulationAxis axis, int sign)
        {
            if (!onlyFace || OnCirculationFace(piece, a, axis, sign) && OnCirculationFace(piece, b, axis, sign))
                output.Add(new SnapFeature(GeometricSnapKind.Edge, piece.Id, index,
                    World(piece, a), World(piece, b), Vector3.zero, Vector3.zero, Vector3.zero));
            index++;
        }

        private static bool UseCirculationSurface(Vector3 normal, bool onlyFace, ManipulationAxis axis, int sign) =>
            !onlyFace || Vector3.Dot(normal, ManipulationSession.AxisVector(axis) * sign) > 0.5f;

        private static void AddCirculationSurface(PieceData piece, List<SnapFeature> output,
            Vector3 center, Vector3 normal, Vector3 u, Vector3 v,
            ref int index, bool onlyFace, ManipulationAxis axis, int sign)
        {
            if (UseCirculationSurface(normal, onlyFace, axis, sign))
                output.Add(new SnapFeature(GeometricSnapKind.Surface, piece.Id, index,
                    World(piece, center), Vector3.zero, piece.Transform.Rotation * normal,
                    piece.Transform.Rotation * u, piece.Transform.Rotation * v));
            index++;
        }

        private static void AddCirculationTriangle(PieceData piece, List<SnapFeature> output,
            Vector3 a, Vector3 b, Vector3 c, Vector3 desiredNormal,
            ref int index, bool onlyFace, ManipulationAxis axis, int sign)
        {
            if (UseCirculationSurface(desiredNormal, onlyFace, axis, sign))
                output.Add(new SnapFeature(GeometricSnapKind.Surface, piece.Id, index,
                    World(piece, (a + b + c) / 3f), Vector3.zero, piece.Transform.Rotation * desiredNormal,
                    piece.Transform.Rotation * (b - a), piece.Transform.Rotation * (c - a), true));
            index++;
        }

        private static void CollectRoof(PieceData piece, List<SnapFeature> output,
            bool onlyFace, ManipulationAxis movingAxis, int faceSign)
        {
            RoofProfile profile = RoofProfile.Create(piece.Dimensions);
            Vector3[] eaves = profile.Eaves;
            Vector3 thickness = Vector3.up * piece.Dimensions.RoofThickness;
            int index = 0;
            for (int i = 0; i < 4; i++)
            {
                AddRoofPoint(piece, output, eaves[i], ref index, onlyFace, movingAxis, faceSign);
                AddRoofPoint(piece, output, eaves[i] - thickness, ref index, onlyFace, movingAxis, faceSign);
            }
            foreach (Vector3 ridge in profile.Ridge)
                AddRoofPoint(piece, output, ridge, ref index, onlyFace, movingAxis, faceSign);
            int edgeIndex = 0;
            for (int i = 0; i < profile.Boundary.Length; i++)
            {
                Vector3 a = profile.Boundary[i], b = profile.Boundary[(i + 1) % profile.Boundary.Length];
                AddRoofEdge(piece, output, a, b, ref edgeIndex, onlyFace, movingAxis, faceSign);
                AddRoofEdge(piece, output, a - thickness, b - thickness,
                    ref edgeIndex, onlyFace, movingAxis, faceSign);
                AddRoofEdge(piece, output, a, a - thickness,
                    ref edgeIndex, onlyFace, movingAxis, faceSign);
            }
            if (piece.Type != PieceType.ShedRoof)
            {
                Vector3[] ridge = profile.Ridge;
                if (ridge.Length == 2)
                    AddRoofEdge(piece, output, ridge[0], ridge[1], ref edgeIndex, onlyFace, movingAxis, faceSign);
                if (piece.Type != PieceType.GableRoof && ridge.Length == 1)
                {
                    for (int i = 0; i < 4; i++)
                        AddRoofEdge(piece, output, eaves[i], ridge[0], ref edgeIndex, onlyFace, movingAxis, faceSign);
                }
                else if (piece.Type != PieceType.GableRoof && piece.Dimensions.X > piece.Dimensions.Z)
                {
                    AddRoofEdge(piece, output, eaves[0], ridge[0], ref edgeIndex, onlyFace, movingAxis, faceSign);
                    AddRoofEdge(piece, output, eaves[3], ridge[0], ref edgeIndex, onlyFace, movingAxis, faceSign);
                    AddRoofEdge(piece, output, eaves[1], ridge[1], ref edgeIndex, onlyFace, movingAxis, faceSign);
                    AddRoofEdge(piece, output, eaves[2], ridge[1], ref edgeIndex, onlyFace, movingAxis, faceSign);
                }
                else if (piece.Type != PieceType.GableRoof)
                {
                    AddRoofEdge(piece, output, eaves[0], ridge[0], ref edgeIndex, onlyFace, movingAxis, faceSign);
                    AddRoofEdge(piece, output, eaves[1], ridge[0], ref edgeIndex, onlyFace, movingAxis, faceSign);
                    AddRoofEdge(piece, output, eaves[2], ridge[1], ref edgeIndex, onlyFace, movingAxis, faceSign);
                    AddRoofEdge(piece, output, eaves[3], ridge[1], ref edgeIndex, onlyFace, movingAxis, faceSign);
                }
            }
            int surfaceIndex = 0;
            foreach (RoofTriangle face in profile.Top)
            {
                if (!onlyFace)
                {
                    AddRoofTriangle(piece, output, face.A, face.B, face.C, ref surfaceIndex, true);
                    AddRoofTriangle(piece, output, face.A - thickness, face.B - thickness,
                        face.C - thickness, ref surfaceIndex, false);
                }
            }
            for (int i = 0; i < profile.Boundary.Length; i++)
            {
                Vector3 a = profile.Boundary[i], b = profile.Boundary[(i + 1) % profile.Boundary.Length];
                Vector3 midpoint = (a + b) * 0.5f;
                if (!onlyFace || OnRoofFace(piece, midpoint, movingAxis, faceSign))
                {
                    Vector3 edge = b - a;
                    Vector3 normal = new Vector3(edge.z, 0f, -edge.x).normalized;
                    output.Add(new SnapFeature(GeometricSnapKind.Surface, piece.Id, surfaceIndex,
                        World(piece, midpoint - thickness * 0.5f), Vector3.zero,
                        piece.Transform.Rotation * normal, piece.Transform.Rotation * ((b - a) * 0.5f),
                        piece.Transform.Rotation * (thickness * 0.5f)));
                }
                surfaceIndex++;
            }
        }

        private static Vector3 World(PieceData piece, Vector3 local) =>
            piece.Transform.Position + piece.Transform.Rotation * local;

        private static bool OnRoofFace(PieceData piece, Vector3 point, ManipulationAxis axis, int sign)
        {
            float extent = axis == ManipulationAxis.X ? piece.Dimensions.X * 0.5f : piece.Dimensions.Z * 0.5f;
            float coordinate = axis == ManipulationAxis.X ? point.x : point.z;
            return axis != ManipulationAxis.Y && Mathf.Abs(coordinate - sign * extent) < 0.0001f;
        }

        private static void AddRoofPoint(PieceData piece, List<SnapFeature> output, Vector3 point,
            ref int index, bool onlyFace, ManipulationAxis axis, int sign)
        {
            if (!onlyFace || OnRoofFace(piece, point, axis, sign))
                output.Add(new SnapFeature(GeometricSnapKind.Endpoint, piece.Id, index,
                    World(piece, point), Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero));
            index++;
        }

        private static void AddRoofEdge(PieceData piece, List<SnapFeature> output, Vector3 a, Vector3 b,
            ref int index, bool onlyFace, ManipulationAxis axis, int sign)
        {
            if (!onlyFace || OnRoofFace(piece, a, axis, sign) && OnRoofFace(piece, b, axis, sign))
                output.Add(new SnapFeature(GeometricSnapKind.Edge, piece.Id, index,
                    World(piece, a), World(piece, b), Vector3.zero, Vector3.zero, Vector3.zero));
            index++;
        }

        private static void AddRoofTriangle(PieceData piece, List<SnapFeature> output,
            Vector3 a, Vector3 b, Vector3 c, ref int index, bool upper)
        {
            Vector3 u = b - a, v = c - a;
            Vector3 normal = Vector3.Cross(u, v).normalized;
            if ((Vector3.Dot(normal, Vector3.up) < 0f) == upper) normal = -normal;
            output.Add(new SnapFeature(GeometricSnapKind.Surface, piece.Id, index++,
                World(piece, (a + b + c) / 3f), Vector3.zero, piece.Transform.Rotation * normal,
                piece.Transform.Rotation * u, piece.Transform.Rotation * v, true));
        }
    }
}
