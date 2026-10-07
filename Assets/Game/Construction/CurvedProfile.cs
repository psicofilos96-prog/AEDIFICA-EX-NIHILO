using System;
using UnityEngine;

namespace Aedifica.Construction
{
    // Shared parametric samples for render geometry and bounded snap features.
    public static class CurvedProfile
    {
        public const int ArchSegments = 24;
        public const int VaultSegments = 24;
        public const int DomeLongitudeSegments = 32;
        public const int DomeLatitudeSegments = 12;

        // Clockwise outline of the solid XY arch section; the central opening has no bottom edge.
        public static Vector2[] ArchOutline(ArchDimensions d)
        {
            if (!d.IsValid) throw new ArgumentException("Valid Arch dimensions required.", nameof(d));
            float half = d.Width * 0.5f, opening = d.OpeningWidth * 0.5f;
            var points = new Vector2[ArchSegments + 7];
            points[0] = new Vector2(-half, 0f);
            points[1] = new Vector2(-half, d.Height);
            points[2] = new Vector2(half, d.Height);
            points[3] = new Vector2(half, 0f);
            points[4] = new Vector2(opening, 0f);
            for (int i = 0; i <= ArchSegments; i++)
            {
                float angle = Mathf.PI * i / ArchSegments;
                points[5 + i] = new Vector2(i == 0 ? opening : i == ArchSegments ? -opening : opening * Mathf.Cos(angle),
                    i == 0 || i == ArchSegments ? d.SpringHeight : d.SpringHeight + d.ArchRise * Mathf.Sin(angle));
            }
            points[ArchSegments + 6] = new Vector2(-opening, 0f);
            return points;
        }

        public static Vector2 VaultOuter(VaultDimensions d, int step) => VaultPoint(d.Width * 0.5f, d.Height, step);
        public static Vector2 VaultInner(VaultDimensions d, int step) => VaultPoint(d.InnerWidth * 0.5f, d.InnerRise, step);
        private static Vector2 VaultPoint(float radius, float rise, int step)
        {
            float angle = Mathf.PI * step / VaultSegments;
            return new Vector2(step == 0 ? radius : step == VaultSegments ? -radius : radius * Mathf.Cos(angle),
                step == 0 || step == VaultSegments ? 0f : rise * Mathf.Sin(angle));
        }

        public static Vector3 DomeOuter(DomeDimensions d, int latitude, int longitude) =>
            DomePoint(d.Diameter * 0.5f, d.Rise, latitude, longitude);
        public static Vector3 DomeInner(DomeDimensions d, int latitude, int longitude) =>
            DomePoint(d.InnerDiameter * 0.5f, d.InnerRise, latitude, longitude);
        private static Vector3 DomePoint(float radius, float rise, int latitude, int longitude)
        {
            float polar = Mathf.PI * 0.5f * latitude / DomeLatitudeSegments;
            float azimuth = Mathf.PI * 2f * longitude / DomeLongitudeSegments;
            float horizontal = latitude == DomeLatitudeSegments ? radius : radius * Mathf.Sin(polar);
            return new Vector3(horizontal * Mathf.Cos(azimuth),
                latitude == DomeLatitudeSegments ? 0f : rise * Mathf.Cos(polar), horizontal * Mathf.Sin(azimuth));
        }
    }
}
