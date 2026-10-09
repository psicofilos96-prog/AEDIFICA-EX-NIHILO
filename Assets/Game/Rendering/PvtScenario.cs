using System;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    public static class PvtScenario
    {
        public const int Seed = 4107;
        public static readonly int[] Sizes = { 10000, 25000, 50000 };

        // Ten architectural pieces per plot; exact same logical input for all render modes.
        public static PieceData PieceAt(int index, int seed = Seed)
        {
            if (index < 0 || index >= Sizes[Sizes.Length - 1])
                throw new ArgumentOutOfRangeException(nameof(index));
            int plot = index / 10, part = index % 10;
            Vector2Int cell = VisualBenchmarkScenario.Cell(plot);
            uint hash = unchecked((uint)seed * 16777619u ^ (uint)plot * 1103515245u);
            Vector3 origin = new Vector3(cell.x * 16f + (hash & 7u) * 0.1f, 0f,
                cell.y * 16f + ((hash >> 3) & 7u) * 0.1f);
            Quaternion rotation = Quaternion.Euler(0f, plot % 4 * 15f, 0f);
            PieceTransform At(Vector3 offset) => new PieceTransform(origin + rotation * offset, rotation);
            PieceId id = PieceId.Parse(new Guid(seed, (short)(index >> 16), (short)index,
                0x50, 0x56, 0x54, 0x31, 0x00, 0x00, 0x00, 0x01).ToString("N"));
            switch (part)
            {
                case 0: return new PieceData(id, At(Vector3.zero), new SlabDimensions(8f, 0.25f, 7f))
                    .WithMaterial(LabMaterialIds.Stone);
                case 1:
                {
                    PieceData wall = new PieceData(id, At(new Vector3(0f, 0.25f, -3.5f)),
                        new WallDimensions(8f, 3.2f, 0.3f)).WithMaterial(LabMaterialIds.Brick);
                    return plot % 3 == 0 ? wall.WithOpening(new WallOpening(
                        new Guid(seed, (short)plot, (short)part, 0x50, 0x56, 0x54, 0x31, 0, 0, 0, 2),
                        id, WallOpeningKind.Passage, 3.3f, 0f, 1.4f, 2.4f)) : wall;
                }
                case 2: return new PieceData(id, At(new Vector3(0f, 0.25f, 3.5f)),
                    new WallDimensions(8f, 3.2f, 0.3f)).WithMaterial(LabMaterialIds.Plaster);
                case 3: return new PieceData(id, At(new Vector3(-4f, 0.25f, 0f)),
                    new WallDimensions(7f, 3.2f, 0.3f)).WithMaterial(LabMaterialIds.Stone);
                case 4: return new PieceData(id, At(new Vector3(4f, 0.25f, 0f)),
                    new WallDimensions(7f, 3.2f, 0.3f)).WithMaterial(LabMaterialIds.Brick);
                case 5: return new PieceData(id, At(new Vector3(0f, 3.45f, 0f)),
                    plot % 2 == 0 ? new PieceDimensions(new GableRoofDimensions(9f, 8f, 0.3f, 1.7f))
                        : new PieceDimensions(new HipRoofDimensions(9f, 8f, 0.3f, 1.7f)))
                    .WithMaterial(LabMaterialIds.Plaster);
                case 6: return new PieceData(id, At(new Vector3(-2f, 0.25f, -3.6f)),
                    new ArchDimensions(3.5f, 3f, 0.7f, 0.6f, 1.2f, 0.25f)).WithMaterial(LabMaterialIds.Stone);
                case 7: return new PieceData(id, At(new Vector3(5.5f, 0.25f, 5.5f)),
                    new ColumnDimensions(1.5f, 7f, 1.5f)).WithMaterial(LabMaterialIds.Brick);
                case 8: return new PieceData(id, At(new Vector3(5.5f, 7.25f, 5.5f)),
                    new ParapetDimensions(2f, 0.9f, 2f)).WithMaterial(LabMaterialIds.Stone);
                default: return new PieceData(id, At(new Vector3(0f, 0.25f, 7f)),
                    new WallDimensions(14f, 2.5f, 0.4f)).WithMaterial(LabMaterialIds.Stone);
            }
        }
    }
}
