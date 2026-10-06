using UnityEngine;

namespace Aedifica.Geometry
{
    public sealed class BlockGeometry
    {
        public Vector3[] Vertices { get; }
        public int[] Triangles { get; }
        public Vector3[] Normals { get; }
        public Vector2[] UVs { get; }
        public Bounds Bounds { get; }

        internal BlockGeometry(Vector3[] vertices, int[] triangles, Vector3[] normals, Vector2[] uvs, Bounds bounds)
        {
            Vertices = vertices;
            Triangles = triangles;
            Normals = normals;
            UVs = uvs;
            Bounds = bounds;
        }
    }
}
