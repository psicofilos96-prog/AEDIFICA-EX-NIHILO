using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Repeats the authored village, including its openings and finishes, without sharing identities.
    public static class PvtGrandVillageScaleScenario
    {
        public static readonly int[] Sizes = { 1000, 5000, 10000, 25000, 50000, 100000 };
        public const float TileSpacing = 180f;

        public static int TileCount(int pieces, int templateCount) =>
            (pieces + templateCount - 1) / templateCount;

        public static Vector3 TileOffset(int tile)
        {
            if (tile < 0) throw new ArgumentOutOfRangeException(nameof(tile));
            // Fixed-width grid keeps existing tiles in place as the scenario grows.
            int row = tile / 16, column = tile % 16;
            return new Vector3(column * TileSpacing, 0f, row * TileSpacing);
        }

        private static Guid Id(int tile, int index, int kind, int feature)
        {
            byte[] bytes = new byte[16];
            Array.Copy(BitConverter.GetBytes(tile), 0, bytes, 0, 4);
            Array.Copy(BitConverter.GetBytes(index), 0, bytes, 4, 4);
            Array.Copy(BitConverter.GetBytes(kind), 0, bytes, 8, 4);
            Array.Copy(BitConverter.GetBytes(feature), 0, bytes, 12, 4);
            bytes[15] ^= 0xA7;
            return new Guid(bytes);
        }

        public static PieceData PieceAt(IReadOnlyList<PieceData> template, int ordinal)
        {
            if (template == null || template.Count == 0) throw new ArgumentException("Village template is empty.");
            if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
            int tile = ordinal / template.Count, index = ordinal % template.Count;
            PieceData source = template[index];
            if (tile == 0) return source;
            PieceId id = PieceId.Parse(Id(tile, index, 0, 0).ToString("N"));
            var result = new PieceData(id, new PieceTransform(
                source.Transform.Position + TileOffset(tile), source.Transform.Rotation),
                source.Dimensions, source.MaterialId);
            var openings = new Dictionary<Guid, Guid>();
            for (int i = 0; i < source.Openings.Count; i++)
            {
                WallOpening opening = source.Openings[i];
                Guid openingId = Id(tile, index, 1, i);
                openings.Add(opening.Id, openingId);
                result = result.WithOpening(new WallOpening(openingId, id, opening.Kind,
                    opening.Left, opening.Bottom, opening.Width, opening.Height));
            }
            for (int i = 0; i < source.Finishes.Count; i++)
            {
                ArchitecturalFinish finish = source.Finishes[i];
                result = result.WithFinish(new ArchitecturalFinish(Id(tile, index, 2, i), id,
                    finish.OpeningId == Guid.Empty ? Guid.Empty : openings[finish.OpeningId],
                    finish.Kind, finish.MaterialId, finish.Profile, finish.Projection));
            }
            return result;
        }
    }
}
