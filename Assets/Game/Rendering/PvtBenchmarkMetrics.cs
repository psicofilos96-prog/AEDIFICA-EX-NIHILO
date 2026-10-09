using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;

namespace Aedifica.Rendering
{
    public static class PvtBenchmarkCsv
    {
        public const string FramesHeader = "utc,commit,unity,platform,environment,cpu,gpu,ram_capacity_mb,vram_capacity_mb,scenario,seed,layout,camera,repeat,pieces,gameobjects,renderers,renderers_visible,meshes,colliders,triangles,regions,screen_width,screen_height,quality,vsync,target_fps,frames,fps_mean,fps_p01,fps_p05,fps_p50,frame_mean_ms,frame_p50_ms,frame_p95_ms,frame_p99_ms,frame_max_ms,stutter_33ms,stutter_50ms,cpu_main_ms,cpu_render_ms,gpu_ms,draw_calls,batches,setpass,managed_mb,unity_reserved_mb,process_rss_mb,vram_used_mb,logical_create_ms,visual_build_ms,status,error";
        public const string EditsHeader = "utc,commit,scenario,seed,layout,repeat,operation,samples,ms_p50,ms_p95,ms_p99,ms_max,integrity,status,error";

        public static string Row(string header, params string[] fields)
        {
            int count = header.Split(',').Length;
            if (fields == null || fields.Length != count)
                throw new ArgumentException($"Expected {count} CSV fields, got {fields?.Length ?? 0}.");
            var quoted = new string[count];
            for (int i = 0; i < count; i++)
                quoted[i] = "\"" + (fields[i] ?? "unavailable").Replace("\"", "\"\"") + "\"";
            return string.Join(",", quoted);
        }

        public static string Number(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? "unavailable"
                : value.ToString("F3", CultureInfo.InvariantCulture);

        public static double Percentile(List<float> sorted, double fraction)
        {
            if (sorted.Count == 0) return double.NaN;
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1,
                (int)Math.Ceiling(sorted.Count * fraction) - 1))];
        }

        public static string Mean(List<float> samples)
        {
            if (samples.Count == 0) return "unavailable";
            double sum = 0d;
            foreach (float sample in samples) sum += sample;
            return Number(sum / samples.Count);
        }
    }

    // Profiler metrics are optional. Invalid/missing counters are reported as unavailable.
    public sealed class PvtFrameCounters : IDisposable
    {
        private ProfilerRecorder main, render, gpu, draws, batches, setPass;
        public readonly List<float> Main = new List<float>();
        public readonly List<float> Render = new List<float>();
        public readonly List<float> Gpu = new List<float>();
        public readonly List<float> Draws = new List<float>();
        public readonly List<float> Batches = new List<float>();
        public readonly List<float> SetPass = new List<float>();

        public PvtFrameCounters()
        {
            main = Start(ProfilerCategory.Internal, "Main Thread");
            render = Start(ProfilerCategory.Internal, "Render Thread");
            gpu = Start(ProfilerCategory.Render, "GPU Frame Time");
            draws = Start(ProfilerCategory.Render, "Draw Calls Count");
            batches = Start(ProfilerCategory.Render, "Batches Count");
            setPass = Start(ProfilerCategory.Render, "SetPass Calls Count");
        }

        private static ProfilerRecorder Start(ProfilerCategory category, string name)
        {
            try { return ProfilerRecorder.StartNew(category, name, 16); }
            catch { return default; }
        }

        private static void Sample(ProfilerRecorder recorder, List<float> destination, float multiplier,
            long maximum = long.MaxValue)
        {
            if (recorder.Valid && recorder.LastValue > 0 && recorder.LastValue < maximum)
                destination.Add(recorder.LastValue * multiplier);
        }

        public void Read()
        {
            Sample(main, Main, 0.000001f, 1000000000L);
            Sample(render, Render, 0.000001f, 1000000000L);
            Sample(gpu, Gpu, 0.000001f, 1000000000L);
            Sample(draws, Draws, 1f);
            Sample(batches, Batches, 1f);
            Sample(setPass, SetPass, 1f);
        }

        public void Dispose()
        {
            if (main.Valid) main.Dispose();
            if (render.Valid) render.Dispose();
            if (gpu.Valid) gpu.Dispose();
            if (draws.Valid) draws.Dispose();
            if (batches.Valid) batches.Dispose();
            if (setPass.Valid) setPass.Dispose();
        }
    }
}
