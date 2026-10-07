using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Geometry
{
    public static class RoofGeometryGenerator
    {
        public static BlockGeometry Generate(PieceDimensions dimensions)
        {
            if (!dimensions.IsSlopedRoof || !dimensions.IsValid)
                throw new ArgumentException("A valid sloped roof is required.", nameof(dimensions));
            RoofProfile profile = RoofProfile.Create(dimensions);
            var vertices = new List<Vector3>(72);
            var normals = new List<Vector3>(72);
            var triangles = new List<int>(72);
            var uvs = new List<Vector2>(72);
            Vector3 thickness = Vector3.up * dimensions.RoofThickness;
            foreach (RoofTriangle face in profile.Top)
            {
                AddTriangle(vertices, normals, triangles, uvs, face.A, face.B, face.C, Vector3.up);
                AddTriangle(vertices, normals, triangles, uvs,
                    face.A - thickness, face.B - thickness, face.C - thickness, Vector3.down);
            }
            for (int i = 0; i < profile.Boundary.Length; i++)
            {
                Vector3 a = profile.Boundary[i], b = profile.Boundary[(i + 1) % profile.Boundary.Length];
                Vector3 edge = b - a;
                Vector3 outward = new Vector3(edge.z, 0f, -edge.x);
                AddTriangle(vertices, normals, triangles, uvs, a, b, b - thickness, outward);
                AddTriangle(vertices, normals, triangles, uvs, a, b - thickness, a - thickness, outward);
            }
            return new BlockGeometry(vertices.ToArray(), triangles.ToArray(), normals.ToArray(), uvs.ToArray(),
                new Bounds(new Vector3(0f, dimensions.Y * 0.5f, 0f),
                    new Vector3(dimensions.X, dimensions.Y, dimensions.Z)));
        }

        private static void AddTriangle(List<Vector3> vertices, List<Vector3> normals,
            List<int> triangles, List<Vector2> uvs, Vector3 a, Vector3 b, Vector3 c, Vector3 desiredNormal)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, desiredNormal) < 0f)
            {
                Vector3 swap = b; b = c; c = swap;
                normal = -normal;
            }
            if (normal.sqrMagnitude <= 0.0000000001f)
                throw new InvalidOperationException("Roof topology contains a degenerate triangle.");
            normal.Normalize();
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++) normals.Add(normal);
            uvs.Add(new Vector2(a.x, a.z)); uvs.Add(new Vector2(b.x, b.z)); uvs.Add(new Vector2(c.x, c.z));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
    }
}
