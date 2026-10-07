using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aedifica.Construction
{
    public readonly struct RoofTriangle
    {
        public readonly Vector3 A, B, C;
        public RoofTriangle(Vector3 a, Vector3 b, Vector3 c) { A = a; B = b; C = c; }
    }

    // Local Y=0 is the lowest underside eave. The roof is a vertical thickness
    // extrusion of a piecewise planar upper surface, not a zero-thickness plane.
    public sealed class RoofProfile
    {
        public Vector3[] Eaves { get; }
        public Vector3[] Boundary { get; }
        public Vector3[] Ridge { get; }
        public RoofTriangle[] Top { get; }

        private RoofProfile(Vector3[] eaves, Vector3[] boundary, Vector3[] ridge, RoofTriangle[] top)
        { Eaves = eaves; Boundary = boundary; Ridge = ridge; Top = top; }

        public static RoofProfile Create(PieceDimensions dimensions)
        {
            if (!dimensions.IsSlopedRoof || !dimensions.IsValid)
                throw new ArgumentException("A valid sloped roof is required.", nameof(dimensions));
            float x = dimensions.X * 0.5f, z = dimensions.Z * 0.5f;
            float t = dimensions.RoofThickness, r = dimensions.Rise;
            var eaves = new[] { new Vector3(-x, t, -z), new Vector3(x, t, -z),
                new Vector3(x, t, z), new Vector3(-x, t, z) };
            var top = new List<RoofTriangle>(8);
            Vector3[] ridge;
            if (dimensions.Type == PieceType.ShedRoof)
            {
                eaves[2].y += r; eaves[3].y += r;
                ridge = new[] { eaves[3], eaves[2] };
                AddQuad(top, eaves[0], eaves[1], eaves[2], eaves[3]);
            }
            else if (dimensions.Type == PieceType.GableRoof)
            {
                Vector3 west = new Vector3(-x, t + r, 0f), east = new Vector3(x, t + r, 0f);
                ridge = new[] { west, east };
                AddQuad(top, eaves[0], eaves[1], east, west);
                AddQuad(top, eaves[3], eaves[2], east, west);
            }
            else if (dimensions.X > dimensions.Z)
            {
                Vector3 west = new Vector3(-x + z, t + r, 0f), east = new Vector3(x - z, t + r, 0f);
                ridge = new[] { west, east };
                AddQuad(top, eaves[0], eaves[1], east, west);
                AddQuad(top, eaves[3], eaves[2], east, west);
                top.Add(new RoofTriangle(eaves[1], eaves[2], east));
                top.Add(new RoofTriangle(eaves[3], eaves[0], west));
            }
            else if (dimensions.Z > dimensions.X)
            {
                Vector3 south = new Vector3(0f, t + r, -z + x), north = new Vector3(0f, t + r, z - x);
                ridge = new[] { south, north };
                AddQuad(top, eaves[0], eaves[3], north, south);
                AddQuad(top, eaves[1], eaves[2], north, south);
                top.Add(new RoofTriangle(eaves[0], eaves[1], south));
                top.Add(new RoofTriangle(eaves[2], eaves[3], north));
            }
            else
            {
                Vector3 apex = new Vector3(0f, t + r, 0f);
                ridge = new[] { apex };
                for (int i = 0; i < 4; i++) top.Add(new RoofTriangle(eaves[i], eaves[(i + 1) % 4], apex));
            }
            // Gable ends follow the two sloping profile edges through the ridge.
            Vector3[] boundary = dimensions.Type == PieceType.GableRoof
                ? new[] { eaves[0], eaves[1], ridge[1], eaves[2], eaves[3], ridge[0] }
                : eaves;
            return new RoofProfile(eaves, boundary, ridge, top.ToArray());
        }

        private static void AddQuad(List<RoofTriangle> top, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            top.Add(new RoofTriangle(a, b, c));
            top.Add(new RoofTriangle(a, c, d));
        }
    }
}
