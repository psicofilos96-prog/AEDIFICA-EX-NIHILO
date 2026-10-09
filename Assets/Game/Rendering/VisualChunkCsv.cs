using System;

namespace Aedifica.Rendering
{
    public static class VisualChunkCsv
    {
        public const string Header = VisualBenchmarkCsv.Header;
        public static readonly string ExtendedHeader = Header.Substring(0, Header.Length - "status,error".Length) +
            "layout_mode,renderers_existing,chunks_existing,chunks_active,chunks_visible,chunks_discarded," +
            "chunk_build_ms,visibility_eval_mean_ms,visibility_evaluations,chunk_state_changes,status,error";
        public static int Columns => ExtendedHeader.Split(',').Length;

        public static string Row(params string[] fields)
        {
            if (fields == null || fields.Length != Columns)
                throw new ArgumentException($"Expected {Columns} E2c.2 CSV fields.", nameof(fields));
            var quoted = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                quoted[i] = "\"" + (fields[i] ?? "unavailable").Replace("\"", "\"\"") + "\"";
            return string.Join(",", quoted);
        }
    }

    public static class VisualChunkBenchmarkModes
    {
        public static readonly string[] Cameras = { "fixed", "path", "overview" };
        public static string[] Layouts(int repeat)
        {
            switch (repeat)
            {
                case 1: return new[] { "baseline", "chunked_no_culling", "chunked_culling" };
                case 2: return new[] { "chunked_culling", "baseline", "chunked_no_culling" };
                default: return new[] { "chunked_no_culling", "chunked_culling", "baseline" };
            }
        }
    }
}
