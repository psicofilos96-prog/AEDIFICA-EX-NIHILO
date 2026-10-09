using System;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    // Stable logical source for the visual benchmark. Every item becomes a real PieceView.
    public static class VisualBenchmarkScenario
    {
        public const int Seed = 2301;
        public static readonly int[] Sizes = { 1000, 5000, 10000, 25000, 50000 };
        public const float HouseSpacing = 14f;

        public static PieceData PieceAt(int index, int seed)
        {
            if (index < 0 || index >= Sizes[Sizes.Length - 1])
                throw new ArgumentOutOfRangeException(nameof(index));
            int house = index / 8;
            int part = index % 8;
            Vector2Int cell = Cell(house);
            uint hash = unchecked((uint)(seed * 16777619) ^ (uint)(house * 1103515245));
            float jitterX = (hash & 255u) / 255f * 0.6f - 0.3f;
            float jitterZ = ((hash >> 8) & 255u) / 255f * 0.6f - 0.3f;
            Vector3 origin = new Vector3(cell.x * HouseSpacing + jitterX, 0f,
                cell.y * HouseSpacing + jitterZ);
            Quaternion rotation = Quaternion.Euler(0f, (house % 4) * 15f, 0f);
            PieceTransform At(Vector3 offset) => new PieceTransform(origin + rotation * offset, rotation);
            PieceId id = PieceId.Parse(new Guid(seed, (short)(index >> 16), (short)index,
                0xe2, 0xc1, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01).ToString("N"));
            switch (part)
            {
                case 0: return new PieceData(id, At(Vector3.zero), new SlabDimensions(8f, 0.2f, 6f))
                    .WithMaterial(LabMaterialIds.Plaster);
                case 1: return new PieceData(id, At(new Vector3(0f, 0.2f, -3f)), new WallDimensions(8f, 3f, 0.25f))
                    .WithMaterial(LabMaterialIds.Stone);
                case 2: return new PieceData(id, At(new Vector3(0f, 0.2f, 3f)), new WallDimensions(8f, 3f, 0.25f))
                    .WithMaterial(LabMaterialIds.Brick);
                case 3: return new PieceData(id, At(new Vector3(-4f, 0.2f, 0f)), new WallDimensions(6f, 3f, 0.25f))
                    .WithMaterial(LabMaterialIds.Stone);
                case 4: return new PieceData(id, At(new Vector3(4f, 0.2f, 0f)), new WallDimensions(6f, 3f, 0.25f))
                    .WithMaterial(LabMaterialIds.Brick);
                case 5: return new PieceData(id, At(new Vector3(0f, 3.2f, 0f)),
                    house % 3 == 0 ? new PieceDimensions(new GableRoofDimensions(9f, 7f, 0.25f, 1.5f))
                    : house % 3 == 1 ? new PieceDimensions(new ShedRoofDimensions(9f, 7f, 0.25f, 1.5f))
                    : new PieceDimensions(new HipRoofDimensions(9f, 7f, 0.25f, 1.5f)))
                    .WithMaterial(LabMaterialIds.Plaster);
                case 6: return new PieceData(id, At(new Vector3(-2f, 0.2f, 0f)), new BlockDimensions(1f, 2f, 1f))
                    .WithMaterial(LabMaterialIds.Neutral);
                default: return new PieceData(id, At(new Vector3(2f, 0.2f, 0f)), new FlatRoofDimensions(3f, 2f, 0.2f))
                    .WithMaterial(LabMaterialIds.Brick);
            }
        }

        public static Vector2Int Cell(int house)
        {
            if (house < 0) throw new ArgumentOutOfRangeException(nameof(house));
            if (house == 0) return Vector2Int.zero;
            int ring = Mathf.CeilToInt((Mathf.Sqrt(house + 1f) - 1f) * 0.5f);
            int side = ring * 2;
            int offset = (side + 1) * (side + 1) - 1 - house;
            if (offset < side) return new Vector2Int(ring - offset, -ring);
            offset -= side;
            if (offset < side) return new Vector2Int(-ring, -ring + offset);
            offset -= side;
            if (offset < side) return new Vector2Int(-ring + offset, ring);
            offset -= side;
            return new Vector2Int(ring, ring - offset);
        }
    }

    public static class VisualBenchmarkRules
    {
        public static bool ExceedsMemory(long managedBytes, long unityReservedBytes, long limitBytes) =>
            limitBytes <= 0 || managedBytes > limitBytes || unityReservedBytes > limitBytes;

        public static bool ExceedsGenerationSeconds(double elapsedSeconds, double limitSeconds) =>
            limitSeconds <= 0d || elapsedSeconds > limitSeconds;

        public static double Percentile(float[] sorted, double quantile) =>
            sorted.Length == 0 ? double.NaN : sorted[Math.Max(0,
                Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * quantile) - 1))];
    }

    public static class VisualBenchmarkCsv
    {
        public const string Header = "utc,commit,unity,platform,environment,cpu,gpu,ram_mb_capacity,vram_mb_capacity,scenario,seed,repeat,camera_mode,pieces,views,renderers_active,renderers_visible,width,height,vsync,target_fps,quality,create_ms,warmup_s,capture_s,frames,fps_mean,fps_median,fps_p01,frame_mean_ms,frame_p50_ms,frame_p95_ms,frame_p99_ms,cpu_mean_ms,gpu_mean_ms,draw_calls_mean,batches_mean,managed_mb,unity_allocated_mb,unity_reserved_mb,gc_collections,gc_allocated_bytes,status,error";
        public static readonly int Columns = Header.Split(',').Length;

        public static string Row(params string[] fields)
        {
            if (fields == null || fields.Length != Columns)
                throw new ArgumentException($"Expected {Columns} CSV columns.", nameof(fields));
            var escaped = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                escaped[i] = "\"" + (fields[i] ?? "unavailable").Replace("\"", "\"\"") + "\"";
            return string.Join(",", escaped);
        }
    }
}
