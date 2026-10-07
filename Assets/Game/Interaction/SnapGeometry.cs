using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Interaction
{
    public enum GeometricSnapKind { Surface, Edge, Endpoint }

    // Transient features derived from PieceData. A/B are endpoints for edges;
    // faces use A as center, Normal and half-extent vectors U/V.
    public readonly struct SnapFeature
    {
        public readonly GeometricSnapKind Kind;
        public readonly PieceId PieceId;
        public readonly int Index;
        public readonly Vector3 A, B, Normal, U, V;

        public SnapFeature(GeometricSnapKind kind, PieceId pieceId, int index,
            Vector3 a, Vector3 b, Vector3 normal, Vector3 u, Vector3 v)
        {
            Kind = kind; PieceId = pieceId; Index = index;
            A = a; B = b; Normal = normal; U = u; V = v;
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
    }
}
