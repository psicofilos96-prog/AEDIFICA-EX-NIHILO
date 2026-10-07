using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Geometry
{
    public static class CirculationGeometryGenerator
    {
        public static BlockGeometry Generate(PieceDimensions dimensions)
        {
            CirculationProfile profile = CirculationProfile.Create(dimensions);
            var vertices = new List<Vector3>(profile.Sections.Length * 42);
            var normals = new List<Vector3>(profile.Sections.Length * 42);
            var triangles = new List<int>(profile.Sections.Length * 42);
            var uvs = new List<Vector2>(profile.Sections.Length * 42);
            float left = -profile.Width * 0.5f, right = profile.Width * 0.5f;
            for (int i = 0; i < profile.Sections.Length; i++)
            {
                CirculationSection section = profile.Sections[i];
                float z0 = section.StartZ, z1 = section.EndZ;
                Quad(vertices, normals, triangles, uvs,
                    new Vector3(left, section.TopStart, z0), new Vector3(right, section.TopStart, z0),
                    new Vector3(right, section.TopEnd, z1), new Vector3(left, section.TopEnd, z1), Vector3.up);
                Quad(vertices, normals, triangles, uvs,
                    new Vector3(left, section.BottomStart, z0), new Vector3(right, section.BottomStart, z0),
                    new Vector3(right, section.BottomEnd, z1), new Vector3(left, section.BottomEnd, z1), Vector3.down);
                foreach (float x in new[] { left, right })
                {
                    Vector3 a = new Vector3(x, section.BottomStart, z0);
                    Vector3 b = new Vector3(x, section.BottomEnd, z1);
                    Vector3 c = new Vector3(x, section.TopEnd, z1);
                    Vector3 d = new Vector3(x, section.TopStart, z0);
                    Vector3 outward = x < 0f ? Vector3.left : Vector3.right;
                    if (profile.IsStair && i > 0)
                    {
                        // Split the left side edge at the previous tread height.
                        // This avoids a T-junction where the next riser meets the side.
                        Vector3 split = new Vector3(x, profile.Sections[i - 1].TopEnd, z0);
                        Triangle(vertices, normals, triangles, uvs, a, b, c, outward);
                        Triangle(vertices, normals, triangles, uvs, a, c, split, outward);
                        Triangle(vertices, normals, triangles, uvs, split, c, d, outward);
                    }
                    else Quad(vertices, normals, triangles, uvs, a, b, c, d, outward);
                }
                if (i == 0)
                    Quad(vertices, normals, triangles, uvs,
                        new Vector3(left, section.BottomStart, z0), new Vector3(right, section.BottomStart, z0),
                        new Vector3(right, section.TopStart, z0), new Vector3(left, section.TopStart, z0), Vector3.back);
                else
                {
                    float previousTop = profile.Sections[i - 1].TopEnd;
                    Quad(vertices, normals, triangles, uvs,
                        new Vector3(left, previousTop, z0), new Vector3(right, previousTop, z0),
                        new Vector3(right, section.TopStart, z0), new Vector3(left, section.TopStart, z0), Vector3.back);
                }
                if (i == profile.Sections.Length - 1)
                    Quad(vertices, normals, triangles, uvs,
                        new Vector3(left, section.BottomEnd, z1), new Vector3(right, section.BottomEnd, z1),
                        new Vector3(right, section.TopEnd, z1), new Vector3(left, section.TopEnd, z1), Vector3.forward);
            }
            return new BlockGeometry(vertices.ToArray(), triangles.ToArray(), normals.ToArray(), uvs.ToArray(),
                new Bounds(new Vector3(0f, profile.TotalHeight * 0.5f, 0f),
                    new Vector3(dimensions.X, profile.TotalHeight, dimensions.Z)));
        }

        private static void Quad(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            List<Vector2> uvs, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 desiredNormal)
        {
            Triangle(vertices, normals, triangles, uvs, a, b, c, desiredNormal);
            Triangle(vertices, normals, triangles, uvs, a, c, d, desiredNormal);
        }

        private static void Triangle(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            List<Vector2> uvs, Vector3 a, Vector3 b, Vector3 c, Vector3 desiredNormal)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, desiredNormal) < 0f)
            {
                Vector3 swap = b; b = c; c = swap;
                normal = -normal;
            }
            if (normal.sqrMagnitude <= 1e-18f)
                throw new InvalidOperationException("Circulation profile contains a degenerate triangle.");
            normal.Normalize();
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++) normals.Add(normal);
            uvs.Add(new Vector2(a.x, a.z)); uvs.Add(new Vector2(b.x, b.z)); uvs.Add(new Vector2(c.x, c.z));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
    }
}
