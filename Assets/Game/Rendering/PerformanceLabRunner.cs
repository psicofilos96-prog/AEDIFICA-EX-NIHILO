using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Aedifica.Construction;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Aedifica.Rendering
{
    // S0/S1 baseline of the present one-PieceView-per-piece path, not an optimized renderer.
    public sealed class PerformanceLabRunner : MonoBehaviour
    {
        private static readonly ProfilerMarker CreateMarker = new ProfilerMarker("Aedifica.PerformanceLab.Create");
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("Aedifica.PerformanceLab.Update");
        [SerializeField] private Material neutralMaterial;
        [SerializeField] private Material stoneMaterial;
        [SerializeField] private Material brickMaterial;
        [SerializeField] private Material plasterMaterial;
        [SerializeField] private bool s1TenHouses;
        [SerializeField] private int seed = 1202;
        [SerializeField] private float warmupSeconds = 30f;
        [SerializeField] private float captureSeconds = 120f;

        private readonly Dictionary<PieceId, PieceView> views = new Dictionary<PieceId, PieceView>();
        private readonly List<float> frameMilliseconds = new List<float>(10000);
        private ConstructionWorld world;
        private int changeCount;
        private double createMilliseconds;
        private double updateMilliseconds;
        private float elapsed;
        private bool exported;
        private string outputPath;

        private void OnValidate()
        {
            warmupSeconds = Mathf.Max(0f, warmupSeconds);
            captureSeconds = Mathf.Max(1f, captureSeconds);
        }

        private void Start()
        {
            if (neutralMaterial == null) { Debug.LogError("PerformanceLab requires the shared URP material.", this); enabled = false; return; }
            var registry = new MaterialRegistry(neutralMaterial, stoneMaterial, brickMaterial, plasterMaterial);
            world = new ConstructionWorld();
            world.Changed += _ => changeCount++;
            Stopwatch clock = Stopwatch.StartNew();
            using (CreateMarker.Auto())
            {
                foreach (PieceData piece in PerformanceLabScenario.Generate(s1TenHouses ? 10 : 1, seed))
                {
                    if (!world.Create(piece).Changed) throw new InvalidOperationException("Duplicate benchmark piece ID.");
                    var visual = new GameObject($"Benchmark {piece.Type} {piece.Id}");
                    visual.transform.SetParent(transform, false);
                    PieceView view = visual.AddComponent<PieceView>();
                    view.Initialize(piece, registry);
                    views.Add(piece.Id, view);
                }
            }
            clock.Stop();
            createMilliseconds = clock.Elapsed.TotalMilliseconds;

            // Measure the existing model + view update path, then restore the same scenario.
            foreach (PieceData piece in world.Pieces)
            {
                Vector3 originalPosition = piece.Transform.Position;
                clock.Restart();
                using (UpdateMarker.Auto())
                {
                    PieceData changed = piece.WithTransform(new PieceTransform(originalPosition + Vector3.right * 0.1f,
                        piece.Transform.Rotation));
                    world.Update(piece.Id, changed);
                    views[piece.Id].Refresh(changed);
                    world.Update(piece.Id, piece);
                    views[piece.Id].Refresh(piece);
                }
                clock.Stop();
                updateMilliseconds = clock.Elapsed.TotalMilliseconds / 2d;
                break;
            }
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera != null)
            {
                camera.transform.position = s1TenHouses ? new Vector3(0f, 48f, -76f) : new Vector3(0f, 12f, -22f);
                camera.transform.LookAt(s1TenHouses ? new Vector3(0f, 0f, 7f) : Vector3.zero);
            }
            Debug.Log($"PerformanceLab {(s1TenHouses ? "S1" : "S0")} ready: seed={seed}, pieces={world.Count}, views={views.Count}. " +
                "Capture starts after warmup; results will be written to persistentDataPath.", this);
        }

        private void Update()
        {
            if (world == null || exported) return;
            float delta = Time.unscaledDeltaTime;
            elapsed += delta;
            if (elapsed >= warmupSeconds && elapsed < warmupSeconds + captureSeconds && delta > 0f)
                frameMilliseconds.Add(delta * 1000f);
            if (elapsed >= warmupSeconds + captureSeconds) Export();
        }

        private void Export()
        {
            exported = true;
            frameMilliseconds.Sort();
            int count = frameMilliseconds.Count;
            double total = 0d, worst = 0d;
            int worstCount = Math.Max(1, (int)Math.Ceiling(count * 0.01d));
            for (int i = 0; i < count; i++) total += frameMilliseconds[i];
            for (int i = count - worstCount; i < count; i++) if (i >= 0) worst += frameMilliseconds[i];
            string commit = Environment.GetEnvironmentVariable("AEDIFICA_COMMIT");
            if (string.IsNullOrEmpty(commit)) commit = "unavailable";
            string scenario = s1TenHouses ? "S1" : "S0";
            outputPath = Path.Combine(Application.persistentDataPath,
                $"aedifica_{scenario}_{seed}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            string[] fields = {
                "utc", "unity", "commit", "scenario", "seed", "platform", "environment", "resolution",
                "cpu", "gpu", "ram_mb_capacity", "vram_mb_capacity", "pieces", "views", "changes",
                "create_ms", "update_ms", "warmup_s", "capture_s", "frames", "fps_mean", "fps_1pct_low",
                "frame_p95_ms", "frame_p99_ms", "unity_allocated_mb", "gpu_frame_ms", "vram_used_mb"
            };
            string N(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
            string[] values = {
                DateTime.UtcNow.ToString("o"), Application.unityVersion, commit, scenario, seed.ToString(),
                Application.platform.ToString(), Application.isEditor ? "Editor (not FPS certification)" : "Player",
                $"{Screen.width}x{Screen.height}", SystemInfo.processorType, SystemInfo.graphicsDeviceName,
                SystemInfo.systemMemorySize > 0 ? SystemInfo.systemMemorySize.ToString() : "unavailable",
                SystemInfo.graphicsMemorySize > 0 ? SystemInfo.graphicsMemorySize.ToString() : "unavailable",
                world.Count.ToString(), views.Count.ToString(), changeCount.ToString(),
                N(createMilliseconds), N(updateMilliseconds), N(warmupSeconds), N(captureSeconds), count.ToString(),
                count == 0 ? "unavailable" : N(1000d * count / total),
                count == 0 ? "unavailable" : N(1000d * worstCount / worst),
                count == 0 ? "unavailable" : N(frameMilliseconds[Math.Min(count - 1, (int)Math.Ceiling(count * 0.95d) - 1)]),
                count == 0 ? "unavailable" : N(frameMilliseconds[Math.Min(count - 1, (int)Math.Ceiling(count * 0.99d) - 1)]),
                N(Profiler.GetTotalAllocatedMemoryLong() / 1048576d), "unavailable", "unavailable"
            };
            using (var writer = new StreamWriter(outputPath))
            {
                writer.WriteLine(string.Join(",", fields));
                for (int i = 0; i < values.Length; i++) values[i] = Csv(values[i]);
                writer.WriteLine(string.Join(",", values));
            }
            Debug.Log($"PerformanceLab result: {outputPath}", this);
        }

        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 760f, 80f), GUI.skin.box);
            GUILayout.Label($"PerformanceLab {(s1TenHouses ? "S1 · 10 houses" : "S0 · 1 house")} | seed {seed} | " +
                (exported ? $"CSV: {outputPath}" : $"{(elapsed < warmupSeconds ? "warmup" : "capture")}: {elapsed:F1}s"));
            GUILayout.Label("Editor results are diagnostic. Certify FPS in a Windows 1080p build on the target notebook.");
            GUILayout.EndArea();
        }
    }
}
