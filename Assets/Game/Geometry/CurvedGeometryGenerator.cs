using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Geometry
{
    public static class CurvedGeometryGenerator
    {
        public static BlockGeometry Generate(PieceDimensions dimensions)
        {
            if (!dimensions.IsValid || !dimensions.IsCurved)
                throw new ArgumentException("Valid curved dimensions required.", nameof(dimensions));
            var mesh = new Builder();
            if (dimensions.IsArch) BuildArch(mesh, dimensions.AsArch());
            else if (dimensions.IsVault) BuildVault(mesh, dimensions.AsVault());
            else BuildDome(mesh, dimensions.AsDome());
            return mesh.Finish(dimensions);
        }

        private static void BuildArch(Builder mesh, ArchDimensions d)
        {
            Vector2[] outline = CurvedProfile.ArchOutline(d);
            var remaining = new List<int>(outline.Length);
            float area = 0f;
            for (int i = 0; i < outline.Length; i++)
            {
                remaining.Add(i);
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                area += a.x * b.y - b.x * a.y;
            }
            float winding = Mathf.Sign(area);
            if (winding == 0f) throw new InvalidOperationException("Arch outline has zero area.");
            int guard = 0;
            while (remaining.Count > 3 && guard++ < outline.Length * outline.Length)
            {
                bool found = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int previous = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int current = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];
                    Vector2 a = outline[previous], b = outline[current], c = outline[next];
                    if (Cross(b - a, c - b) * winding <= 1e-8f) continue;
                    bool contains = false;
                    foreach (int index in remaining)
                    {
                        if (index == previous || index == current || index == next) continue;
                        if (InsideTriangle(outline[index], a, b, c, winding)) { contains = true; break; }
                    }
                    if (contains) continue;
                    mesh.Triangle(new Vector3(a.x, a.y, -d.Depth * 0.5f), new Vector3(b.x, b.y, -d.Depth * 0.5f),
                        new Vector3(c.x, c.y, -d.Depth * 0.5f), Vector3.back);
                    mesh.Triangle(new Vector3(a.x, a.y, d.Depth * 0.5f), new Vector3(b.x, b.y, d.Depth * 0.5f),
                        new Vector3(c.x, c.y, d.Depth * 0.5f), Vector3.forward);
                    remaining.RemoveAt(i);
                    found = true;
                    break;
                }
                if (!found) throw new InvalidOperationException("Arch outline cannot be triangulated.");
            }
            if (remaining.Count != 3) throw new InvalidOperationException("Arch triangulation did not terminate.");
            for (int end = 0; end < 2; end++)
            {
                float z = end == 0 ? -d.Depth * 0.5f : d.Depth * 0.5f;
                mesh.Triangle(new Vector3(outline[remaining[0]].x, outline[remaining[0]].y, z),
                    new Vector3(outline[remaining[1]].x, outline[remaining[1]].y, z),
                    new Vector3(outline[remaining[2]].x, outline[remaining[2]].y, z),
                    end == 0 ? Vector3.back : Vector3.forward);
            }
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                Vector3 outward = new Vector3(winding * (b.y - a.y), -winding * (b.x - a.x), 0f);
                mesh.Quad(new Vector3(a.x, a.y, -d.Depth * 0.5f), new Vector3(a.x, a.y, d.Depth * 0.5f),
                    new Vector3(b.x, b.y, d.Depth * 0.5f), new Vector3(b.x, b.y, -d.Depth * 0.5f), outward);
            }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool InsideTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float winding) =>
            Cross(b - a, p - a) * winding > 1e-7f &&
            Cross(c - b, p - b) * winding > 1e-7f &&
            Cross(a - c, p - c) * winding > 1e-7f;

        private static void BuildVault(Builder mesh, VaultDimensions d)
        {
            float half = d.Length * 0.5f;
            for (int i = 0; i < CurvedProfile.VaultSegments; i++)
            {
                Vector2 o0 = CurvedProfile.VaultOuter(d, i), o1 = CurvedProfile.VaultOuter(d, i + 1);
                Vector2 n0 = CurvedProfile.VaultInner(d, i), n1 = CurvedProfile.VaultInner(d, i + 1);
                Vector3 radial = new Vector3((o0.x + o1.x) / d.Width, (o0.y + o1.y) / d.Height, 0f);
                mesh.Quad(At(o0, -half), At(o0, half), At(o1, half), At(o1, -half), radial);
                mesh.Quad(At(n0, -half), At(n1, -half), At(n1, half), At(n0, half), -radial);
                mesh.Quad(At(o0, -half), At(o1, -half), At(n1, -half), At(n0, -half), Vector3.back);
                mesh.Quad(At(o0, half), At(n0, half), At(n1, half), At(o1, half), Vector3.forward);
                if (i == 0 || i == CurvedProfile.VaultSegments - 1)
                {
                    Vector2 outer = i == 0 ? o0 : o1, inner = i == 0 ? n0 : n1;
                    mesh.Quad(At(outer, -half), At(inner, -half), At(inner, half), At(outer, half),
                        Vector3.down);
                }
            }
        }
        private static Vector3 At(Vector2 point, float z) => new Vector3(point.x, point.y, z);

        private static void BuildDome(Builder mesh, DomeDimensions d)
        {
            int latitudes = CurvedProfile.DomeLatitudeSegments;
            int longitudes = CurvedProfile.DomeLongitudeSegments;
            Vector3 outerApex = CurvedProfile.DomeOuter(d, 0, 0);
            Vector3 innerApex = CurvedProfile.DomeInner(d, 0, 0);
            for (int latitude = 0; latitude < latitudes; latitude++)
            for (int longitude = 0; longitude < longitudes; longitude++)
            {
                int next = (longitude + 1) % longitudes;
                Vector3 outer0 = CurvedProfile.DomeOuter(d, latitude, longitude);
                Vector3 outer1 = CurvedProfile.DomeOuter(d, latitude, next);
                Vector3 outer2 = CurvedProfile.DomeOuter(d, latitude + 1, next);
                Vector3 outer3 = CurvedProfile.DomeOuter(d, latitude + 1, longitude);
                Vector3 inner0 = CurvedProfile.DomeInner(d, latitude, longitude);
                Vector3 inner1 = CurvedProfile.DomeInner(d, latitude, next);
                Vector3 inner2 = CurvedProfile.DomeInner(d, latitude + 1, next);
                Vector3 inner3 = CurvedProfile.DomeInner(d, latitude + 1, longitude);
                Vector3 outward = new Vector3(outer2.x + outer3.x, outer2.y, outer2.z + outer3.z).normalized;
                if (latitude == 0)
                {
                    mesh.Triangle(outerApex, outer2, outer3, outward);
                    mesh.Triangle(innerApex, inner3, inner2, -outward);
                }
                else
                {
                    mesh.Quad(outer0, outer1, outer2, outer3, outward);
                    mesh.Quad(inner0, inner3, inner2, inner1, -outward);
                }
                if (latitude == latitudes - 1)
                    mesh.Quad(outer3, outer2, inner2, inner3, Vector3.down);
            }
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>(2048);
            private readonly List<Vector3> normals = new List<Vector3>(2048);
            private readonly List<int> triangles = new List<int>(2048);
            private readonly List<Vector2> uvs = new List<Vector2>(2048);
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 desired)
            { Triangle(a, b, c, desired); Triangle(a, c, d, desired); }
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 desired)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, desired) < 0f)
                { Vector3 swap = b; b = c; c = swap; normal = -normal; }
                if (normal.sqrMagnitude <= 1e-18f || float.IsInfinity(normal.sqrMagnitude))
                    throw new InvalidOperationException("Curved profile contains a degenerate triangle.");
                normal.Normalize();
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                uvs.Add(new Vector2(a.x, a.z)); uvs.Add(new Vector2(b.x, b.z)); uvs.Add(new Vector2(c.x, c.z));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            public BlockGeometry Finish(PieceDimensions d) => new BlockGeometry(vertices.ToArray(), triangles.ToArray(),
                normals.ToArray(), uvs.ToArray(), new Bounds(new Vector3(0f, d.Y * 0.5f, 0f), new Vector3(d.X, d.Y, d.Z)));
        }
    }
}
