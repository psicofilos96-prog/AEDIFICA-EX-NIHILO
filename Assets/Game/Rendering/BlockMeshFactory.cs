using System;
using Aedifica.Geometry;
using UnityEngine;

namespace Aedifica.Rendering
{
    public static class BlockMeshFactory
    {
        public static Mesh Build(BlockGeometry geometry)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            var mesh = new Mesh { name = "Parametric Block" };
            if (geometry.Vertices.Length > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = geometry.Vertices;
            mesh.triangles = geometry.Triangles;
            mesh.normals = geometry.Normals;
            mesh.uv = geometry.UVs;
            mesh.bounds = geometry.Bounds;
            return mesh;
        }
    }
}
