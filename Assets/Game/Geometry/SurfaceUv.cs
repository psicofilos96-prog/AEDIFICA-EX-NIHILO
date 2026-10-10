using UnityEngine;

namespace Aedifica.Geometry
{
    // Meter-scaled planar coordinates on the dominant face plane. XZ-only mapping
    // collapses vertical roof fascias, vault ends and stair risers to a line.
    internal static class SurfaceUv
    {
        public static Vector2 Project(Vector3 point, Vector3 normal)
        {
            float x=Mathf.Abs(normal.x), y=Mathf.Abs(normal.y), z=Mathf.Abs(normal.z);
            if (y>=x && y>=z) return new Vector2(point.x,point.z);
            if (x>=z) return new Vector2(point.z,point.y);
            return new Vector2(point.x,point.y);
        }
    }
}
