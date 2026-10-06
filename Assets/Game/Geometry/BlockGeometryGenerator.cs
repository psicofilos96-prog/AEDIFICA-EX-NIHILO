using System;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Geometry
{
    public static class BlockGeometryGenerator
    {
        public static BlockGeometry Generate(BlockDimensions dimensions)
        {
            if (!dimensions.IsValid) throw new ArgumentException("Block dimensions must be valid.", nameof(dimensions));

            float x = dimensions.Width * 0.5f;
            float y = dimensions.Height;
            float z = dimensions.Depth * 0.5f;
            var vertices = new Vector3[24];
            var triangles = new int[36];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            int face = 0;

            AddFace(vertices, triangles, normals, uvs, face++, new Vector3(x, 0, -z), new Vector3(-x, 0, -z), new Vector3(-x, y, -z), new Vector3(x, y, -z), Vector3.back, dimensions.Width, y);
            AddFace(vertices, triangles, normals, uvs, face++, new Vector3(-x, 0, z), new Vector3(x, 0, z), new Vector3(x, y, z), new Vector3(-x, y, z), Vector3.forward, dimensions.Width, y);
            AddFace(vertices, triangles, normals, uvs, face++, new Vector3(x, 0, z), new Vector3(x, 0, -z), new Vector3(x, y, -z), new Vector3(x, y, z), Vector3.right, dimensions.Depth, y);
            AddFace(vertices, triangles, normals, uvs, face++, new Vector3(-x, 0, -z), new Vector3(-x, 0, z), new Vector3(-x, y, z), new Vector3(-x, y, -z), Vector3.left, dimensions.Depth, y);
            AddFace(vertices, triangles, normals, uvs, face++, new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, y, -z), new Vector3(-x, y, -z), Vector3.up, dimensions.Width, dimensions.Depth);
            AddFace(vertices, triangles, normals, uvs, face, new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(x, 0, z), new Vector3(-x, 0, z), Vector3.down, dimensions.Width, dimensions.Depth);

            return new BlockGeometry(vertices, triangles, normals, uvs,
                new Bounds(new Vector3(0f, y * 0.5f, 0f), new Vector3(dimensions.Width, y, dimensions.Depth)));
        }

        private static void AddFace(Vector3[] vertices, int[] triangles, Vector3[] normals, Vector2[] uvs, int face,
            Vector3 bottomLeft, Vector3 bottomRight, Vector3 topRight, Vector3 topLeft, Vector3 normal, float uMeters, float vMeters)
        {
            int vertex = face * 4;
            int index = face * 6;
            vertices[vertex] = bottomLeft;
            vertices[vertex + 1] = bottomRight;
            vertices[vertex + 2] = topRight;
            vertices[vertex + 3] = topLeft;
            for (int i = 0; i < 4; i++) normals[vertex + i] = normal;
            uvs[vertex] = Vector2.zero;
            uvs[vertex + 1] = new Vector2(uMeters, 0f);
            uvs[vertex + 2] = new Vector2(uMeters, vMeters);
            uvs[vertex + 3] = new Vector2(0f, vMeters);
            triangles[index] = vertex;
            triangles[index + 1] = vertex + 1;
            triangles[index + 2] = vertex + 2;
            triangles[index + 3] = vertex;
            triangles[index + 4] = vertex + 2;
            triangles[index + 5] = vertex + 3;
        }
    }
}
