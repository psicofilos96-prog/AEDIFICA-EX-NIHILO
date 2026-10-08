using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Stable S0/S1 data, independent of frame order and scene hierarchy.
    public static class PerformanceLabScenario
    {
        public static IEnumerable<PieceData> Generate(int houses, int seed)
        {
            if (houses != 1 && houses != 10) throw new ArgumentOutOfRangeException(nameof(houses));
            uint state = (uint)seed ^ 0x9e3779b9u;
            float Jitter()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return ((state & 0xffffu) / 65535f - 0.5f) * 0.8f;
            }
            for (int house = 0; house < houses; house++)
            {
                float x = (house % 5 - (houses == 1 ? 0 : 2)) * 12f + Jitter();
                float z = house / 5 * 14f + Jitter();
                Quaternion pose = Quaternion.Euler(0f, house % 3 * 15f, 0f);
                Vector3 origin = new Vector3(x, 0f, z);
                PieceId Id(int part) => PieceId.Parse(new Guid(seed, (short)house, (short)part,
                    0xa1, 0xed, 0x1f, 0x0c, 0x00, 0x00, 0x00, 0x01).ToString("N"));
                PieceTransform At(Vector3 offset) => new PieceTransform(origin + pose * offset, pose);
                yield return new PieceData(Id(0), At(Vector3.zero), new SlabDimensions(8f, 0.2f, 6f))
                    .WithMaterial(LabMaterialIds.Plaster);
                yield return new PieceData(Id(1), At(new Vector3(0f, 0.2f, -3f)), new WallDimensions(8f, 3f, 0.25f))
                    .WithMaterial(LabMaterialIds.Stone);
                yield return new PieceData(Id(2), At(new Vector3(0f, 0.2f, 3f)), new WallDimensions(8f, 3f, 0.25f))
                    .WithMaterial(LabMaterialIds.Brick);
                Quaternion side = pose * Quaternion.Euler(0f, 90f, 0f);
                yield return new PieceData(Id(3), new PieceTransform(origin + pose * new Vector3(-4f, 0.2f, 0f), side),
                    new WallDimensions(6f, 3f, 0.25f)).WithMaterial(LabMaterialIds.Stone);
                yield return new PieceData(Id(4), new PieceTransform(origin + pose * new Vector3(4f, 0.2f, 0f), side),
                    new WallDimensions(6f, 3f, 0.25f)).WithMaterial(LabMaterialIds.Brick);
                yield return new PieceData(Id(5), At(new Vector3(0f, 3.2f, 0f)),
                    new GableRoofDimensions(9f, 7f, 0.25f, 1.5f)).WithMaterial(LabMaterialIds.Plaster);
                yield return new PieceData(Id(6), At(new Vector3(-2f, 0.2f, 0f)),
                    new ColumnDimensions(0.5f, 3f, 0.5f)).WithMaterial(LabMaterialIds.Stone);
                yield return new PieceData(Id(7), At(new Vector3(2f, 0.2f, 0f)),
                    new BeamDimensions(3f, 0.4f, 0.4f)).WithMaterial(LabMaterialIds.Brick);
            }
        }
    }
}
