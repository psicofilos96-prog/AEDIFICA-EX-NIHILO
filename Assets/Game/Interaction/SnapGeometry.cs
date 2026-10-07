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
