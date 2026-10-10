using System;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // The same architectural families as PVT-1, fitted inside the 1 km terrain.
    public static class Pvt2Scenario
    {
        public const int Seed = PvtScenario.Seed;
        public const float Extent = 500f;
        public const float PlotSpacing = 14f;
        public static readonly int[] Sizes = { 10000, 25000, 50000 };

        public static float RiverX(float z) => -350f + 30f * Mathf.Sin(z * 0.006f);

        public static float GroundHeight(float x, float z)
        {
            float hills = 12f + 3f * Mathf.Sin(x * 0.009f) * Mathf.Cos(z * 0.011f)
                + 2f * Mathf.Sin(z * 0.021f);
            float riverOffset = (x - RiverX(z)) / 24f;
            return hills - 7f * Mathf.Exp(-riverOffset * riverOffset);
        }

        public static PieceData PieceAt(int index, int seed = Seed)
        {
            PieceData source = PvtScenario.PieceAt(index, seed);
            int plot = index / 10;
            Vector2Int cell = VisualBenchmarkScenario.Cell(plot);
            Vector3 oldAnchor = PvtScenario.PieceAt(plot * 10, seed).Transform.Position;
            Vector3 local = source.Transform.Position - oldAnchor;
            Vector3 newAnchor = new Vector3(
                cell.x * PlotSpacing + oldAnchor.x - cell.x * 16f,
                GroundHeight(cell.x * PlotSpacing, cell.y * PlotSpacing),
                cell.y * PlotSpacing + oldAnchor.z - cell.y * 16f);
            Vector3 position = newAnchor + local;
            if (Mathf.Abs(position.x) > Extent || Mathf.Abs(position.z) > Extent)
                throw new InvalidOperationException("A PVT-2 piece lies outside the terrain.");
            return source.WithTransform(new PieceTransform(position, source.Transform.Rotation));
        }
    }
}
