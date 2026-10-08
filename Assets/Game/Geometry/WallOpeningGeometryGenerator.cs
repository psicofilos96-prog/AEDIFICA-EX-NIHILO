using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Geometry
{
    // A rectangular XY arrangement is extruded through the wall thickness.
    // Each solid cell gets front/back faces; only exposed cell edges get sides.
    public static class WallOpeningGeometryGenerator
    {
        public static BlockGeometry Generate(PieceData wall)
        {
            if (wall == null || wall.Type != PieceType.Wall)
                throw new ArgumentException("A wall is required.", nameof(wall));
            if (wall.Openings.Count == 0) return BlockGeometryGenerator.GeneratePiece(wall.Dimensions);
            float halfLength = wall.Dimensions.X * 0.5f;
            float halfThickness = wall.Dimensions.Z * 0.5f;
            float height = wall.Dimensions.Y;
            var xs = new List<float> { -halfLength, halfLength };
            var ys = new List<float> { 0f, height };
            foreach (WallOpening opening in wall.Openings)
            {
                xs.Add(opening.Left - halfLength);
                xs.Add(opening.Right - halfLength);
                ys.Add(opening.Bottom);
                ys.Add(opening.Top);
            }
            xs.Sort();
            ys.Sort();
            RemoveDuplicates(xs);
            RemoveDuplicates(ys);
            int nx = xs.Count - 1;
            int ny = ys.Count - 1;
            var solid = new bool[nx, ny];
            for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
            {
                float centerX = (xs[x] + xs[x + 1]) * 0.5f + halfLength;
                float centerY = (ys[y] + ys[y + 1]) * 0.5f;
                solid[x, y] = true;
                foreach (WallOpening opening in wall.Openings)
                    if (centerX > opening.Left && centerX < opening.Right &&
                        centerY > opening.Bottom && centerY < opening.Top)
                    {
                        solid[x, y] = false;
                        break;
                    }
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            float front = halfThickness;
            float back = -halfThickness;
            for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
            {
                if (!solid[x, y]) continue;
                float x0 = xs[x], x1 = xs[x + 1], y0 = ys[y], y1 = ys[y + 1];
                AddQuad(vertices, triangles, normals, uvs,
                    new Vector3(x0, y0, front), new Vector3(x1, y0, front),
                    new Vector3(x1, y1, front), new Vector3(x0, y1, front), Vector3.forward);
                AddQuad(vertices, triangles, normals, uvs,
                    new Vector3(x1, y0, back), new Vector3(x0, y0, back),
                    new Vector3(x0, y1, back), new Vector3(x1, y1, back), Vector3.back);
                if (x == 0 || !solid[x - 1, y])
                    AddQuad(vertices, triangles, normals, uvs,
                        new Vector3(x0, y0, back), new Vector3(x0, y0, front),
                        new Vector3(x0, y1, front), new Vector3(x0, y1, back), Vector3.left);
                if (x == nx - 1 || !solid[x + 1, y])
                    AddQuad(vertices, triangles, normals, uvs,
                        new Vector3(x1, y0, front), new Vector3(x1, y0, back),
                        new Vector3(x1, y1, back), new Vector3(x1, y1, front), Vector3.right);
                if (y == 0 || !solid[x, y - 1])
                    AddQuad(vertices, triangles, normals, uvs,
                        new Vector3(x0, y0, back), new Vector3(x1, y0, back),
                        new Vector3(x1, y0, front), new Vector3(x0, y0, front), Vector3.down);
                if (y == ny - 1 || !solid[x, y + 1])
                    AddQuad(vertices, triangles, normals, uvs,
                        new Vector3(x0, y1, front), new Vector3(x1, y1, front),
                        new Vector3(x1, y1, back), new Vector3(x0, y1, back), Vector3.up);
            }
            return new BlockGeometry(vertices.ToArray(), triangles.ToArray(), normals.ToArray(), uvs.ToArray(),
                new Bounds(new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(wall.Dimensions.X, height, wall.Dimensions.Z)));
        }

        private static void RemoveDuplicates(List<float> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
                if (values[i] == values[i - 1]) values.RemoveAt(i);
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles,
            List<Vector3> normals, List<Vector2> uvs, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            float u = Vector3.Distance(a, b);
            float v = Vector3.Distance(b, c);
            uvs.Add(Vector2.zero); uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, v)); uvs.Add(new Vector2(0f, v));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
    }
}
