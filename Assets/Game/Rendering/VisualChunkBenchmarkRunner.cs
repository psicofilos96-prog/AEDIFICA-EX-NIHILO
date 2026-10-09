using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Aedifica.Construction;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Aedifica.Rendering
{
    // Same physical workload as E2c.1, with three explicit visual-layout conditions.
    public sealed class VisualChunkBenchmarkRunner : MonoBehaviour
    {
        [SerializeField] private Camera benchmarkCamera;
        [SerializeField] private Material neutralMaterial, stoneMaterial, brickMaterial, plasterMaterial;
        [SerializeField] private int seed = VisualBenchmarkScenario.Seed;
        [SerializeField] private int buildBatchSize = 32;
        [SerializeField] private int maxUnityReservedMB = 4096;
        [SerializeField] private float maxGenerationSeconds = 1200f;
        [SerializeField] private float warmupSeconds = 15f;
        [SerializeField] private float captureSeconds = 30f;
        [SerializeField] private float maxCaptureSeconds = 120f;
        [SerializeField] private float visualChunkSize = 64f;
        [SerializeField] private float cullingReleaseSeconds = 0.2f;

        private static readonly Func<long> ThreadAllocationCounter = FindThreadAllocationCounter();
        private readonly List<PieceView> views = new List<PieceView>(50000);
        private ConstructionWorld world;
        private MaterialRegistry registry;
        private string outputPath;
        private double cumulativeCreateMilliseconds;
        private VisualChunkManager chunks;
        private string layoutMode = "baseline";
        private double chunkBuildMilliseconds;
        private double visibilityMilliseconds;
        private int visibilitySamples;
        private long stateChangesBefore;
        private string layoutFailure;
        private int snapshotChunksExisting, snapshotChunksActive, snapshotChunksVisible;
        private long snapshotChunksDiscarded;

        public void Configure(Camera camera, Material neutral, Material stone, Material brick, Material plaster)
        {
            benchmarkCamera = camera;
            neutralMaterial = neutral;
            stoneMaterial = stone;
            brickMaterial = brick;
            plasterMaterial = plaster;
        }

        private IEnumerator Start()
        {
            outputPath = Path.Combine(Application.persistentDataPath,
                $"aedifica_E2c2_chunks_{seed}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.csv");
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(outputPath, VisualChunkCsv.ExtendedHeader + Environment.NewLine);
            if (benchmarkCamera == null || neutralMaterial == null || buildBatchSize < 1 ||
                maxUnityReservedMB < 1 || maxGenerationSeconds <= 0f || warmupSeconds < 0f ||
                captureSeconds <= 0f || maxCaptureSeconds <= 0f || captureSeconds > maxCaptureSeconds ||
                visualChunkSize <= 0f || cullingReleaseSeconds < 0f)
            {
                WriteUnavailable(0, 0, "setup", "failed", "Invalid camera, material or safety/capture configuration.");
                Debug.LogError($"E2c.2 benchmark failed: invalid configuration; csv={outputPath}", this);
                yield break;
            }
            registry = new MaterialRegistry(neutralMaterial, stoneMaterial, brickMaterial, plasterMaterial);
            world = new ConstructionWorld();
            if (!Application.isEditor) Screen.SetResolution(1920, 1080, false);
            Debug.Log($"E2c.2 benchmark begin: seed={seed}, target=1920x1080, " +
                $"actual={Screen.width}x{Screen.height}, vsync={QualitySettings.vSyncCount}, " +
                $"targetFps={Application.targetFrameRate}, csv={outputPath}", this);

            for (int scenarioIndex = 0; scenarioIndex < VisualBenchmarkScenario.Sizes.Length; scenarioIndex++)
            {
                int target = VisualBenchmarkScenario.Sizes[scenarioIndex];
                Debug.Log($"E2c.2 scenario begin: V{scenarioIndex + 1}, targetPieces={target}, existing={views.Count}", this);
                string failure = null;
                Stopwatch generationWall = Stopwatch.StartNew();
                while (views.Count < target)
                {
                    if (VisualBenchmarkRules.ExceedsGenerationSeconds(generationWall.Elapsed.TotalSeconds,
                        maxGenerationSeconds)) { failure = $"Generation exceeded {maxGenerationSeconds:F0} seconds."; break; }
                    if (VisualBenchmarkRules.ExceedsMemory(GC.GetTotalMemory(false),
                        Profiler.GetTotalReservedMemoryLong(), maxUnityReservedMB * 1048576L))
                    { failure = $"Memory limit {maxUnityReservedMB} MB reached before {target} views."; break; }
                    int end = Math.Min(target, views.Count + buildBatchSize);
                    Stopwatch batch = Stopwatch.StartNew();
                    try
                    {
                        while (views.Count < end)
                        {
                            PieceData piece = VisualBenchmarkScenario.PieceAt(views.Count, seed);
                            if (!world.Create(piece).Changed) throw new InvalidOperationException("Duplicate visual benchmark ID.");
                            var visual = new GameObject($"Visual {piece.Type} {piece.Id}");
                            visual.transform.SetParent(transform, false);
                            try
                            {
                                PieceView view = visual.AddComponent<PieceView>();
                                view.Initialize(piece, registry);
                                views.Add(view);
                            }
                            catch { Destroy(visual); throw; }
                        }
                    }
                    catch (Exception error) { failure = error.GetType().Name + ": " + error.Message; }
                    batch.Stop();
                    cumulativeCreateMilliseconds += batch.Elapsed.TotalMilliseconds;
                    if (failure != null) break;
                    if (views.Count % 1024 < buildBatchSize)
                        Debug.Log($"E2c.2 generation progress: V{scenarioIndex + 1}, views={views.Count}/{target}, " +
                            $"reservedMB={Profiler.GetTotalReservedMemoryLong() / 1048576}", this);
                    yield return null; // allow rendering and interruption between bounded batches
                }
                if (failure != null)
                {
                    string reason = $"V{scenarioIndex + 1} stopped at {views.Count}/{target}: {failure}";
                    Debug.LogError($"E2c.2 scenario failed: {reason}; csv={outputPath}", this);
                    for (int remaining = scenarioIndex; remaining < VisualBenchmarkScenario.Sizes.Length; remaining++)
                        for (int repeat = 1; repeat <= 3; repeat++)
                            foreach (string layout in VisualChunkBenchmarkModes.Layouts(repeat))
                                foreach (string cameraMode in VisualChunkBenchmarkModes.Cameras)
                                {
                                    layoutMode = layout;
                                    WriteUnavailable(VisualBenchmarkScenario.Sizes[remaining], repeat, cameraMode,
                                        remaining == scenarioIndex ? "failed" : "skipped", reason);
                                }
                    yield return Cleanup();
                    yield break;
                }

                for (int repeat = 1; repeat <= 3; repeat++)
                foreach (string layout in VisualChunkBenchmarkModes.Layouts(repeat))
                {
                    layoutMode = layout;
                    yield return PrepareLayout(layout);
                    if (layoutFailure != null)
                    {
                        string reason = $"V{scenarioIndex + 1} layout {layout}: {layoutFailure}";
                        Debug.LogError($"E2c.2 layout failed: {reason}; csv={outputPath}", this);
                        WriteRemaining(scenarioIndex, repeat, layout, null, reason);
                        yield return Cleanup();
                        yield break;
                    }
                    foreach (string mode in VisualChunkBenchmarkModes.Cameras)
                    {
                    Debug.Log($"E2c.2 capture begin: V{scenarioIndex + 1}, repeat={repeat}, " +
                        $"layout={layout}, camera={mode}, pieces={world.Count}, views={views.Count}", this);
                    PositionCamera(mode, 0f);
                    float warmup = 0f;
                    while (warmup < warmupSeconds)
                    {
                        if (mode == "path") PositionCamera(mode, warmupSeconds > 0f ? warmup / warmupSeconds : 0f);
                        if (layout == "chunked_culling") chunks.Evaluate(benchmarkCamera, Time.realtimeSinceStartup);
                        yield return null;
                        warmup += Time.unscaledDeltaTime;
                    }
                    PositionCamera(mode, 0f);
                    if (layout == "chunked_culling") chunks.Evaluate(benchmarkCamera, Time.realtimeSinceStartup);
                    yield return null; // let Renderer.isVisible reflect the reset camera pose
                    snapshotChunksExisting = chunks != null ? chunks.ChunkCount : 0;
                    snapshotChunksActive = chunks != null ? chunks.ActiveChunkCount : 0;
                    snapshotChunksVisible = chunks != null ? chunks.VisibleChunkCount : 0;
                    snapshotChunksDiscarded = chunks != null ? chunks.DiscardedChunks : 0;
                    int activeRenderers = 0, visibleRenderers = 0;
                    foreach (PieceView view in views)
                    {
                        MeshRenderer renderer = view.GetComponent<MeshRenderer>();
                        if (!renderer.enabled || !view.gameObject.activeInHierarchy) continue;
                        activeRenderers++;
                        if (renderer.isVisible) visibleRenderers++;
                    }
                    var frameTimes = new List<float>(8192);
                    var cpuTimes = new List<float>(8192);
                    var gpuTimes = new List<float>(8192);
                    var draws = new List<float>(8192);
                    var batches = new List<float>(8192);
                    int gcBefore = GC.CollectionCount(0);
                    long? allocatedBefore = ThreadAllocated();
                    ProfilerRecorder cpu = StartCounter(ProfilerCategory.Internal, "Main Thread");
                    ProfilerRecorder gpu = StartCounter(ProfilerCategory.Render, "GPU Frame Time");
                    ProfilerRecorder draw = StartCounter(ProfilerCategory.Render, "Draw Calls Count");
                    ProfilerRecorder batchCounter = StartCounter(ProfilerCategory.Render, "Batches Count");
                    visibilityMilliseconds = 0d;
                    visibilitySamples = 0;
                    stateChangesBefore = chunks != null ? chunks.StateChanges : 0L;
                    // Exclude the renderer enumeration and recorder setup from the first frame sample.
                    yield return null;
                    float elapsed = 0f;
                    Stopwatch captureWall = Stopwatch.StartNew();
                    string captureFailure = null;
                    while (elapsed < captureSeconds)
                    {
                        if (captureWall.Elapsed.TotalSeconds > maxCaptureSeconds)
                        {
                            captureFailure = $"Capture exceeded wall-time limit of {maxCaptureSeconds:F0} seconds.";
                            break;
                        }
                        if (mode == "path") PositionCamera(mode, elapsed / captureSeconds);
                        if (layout == "chunked_culling")
                        {
                            Stopwatch evaluation = Stopwatch.StartNew();
                            chunks.Evaluate(benchmarkCamera, Time.realtimeSinceStartup);
                            evaluation.Stop();
                            visibilityMilliseconds += evaluation.Elapsed.TotalMilliseconds;
                            visibilitySamples++;
                        }
                        yield return null;
                        float dt = Time.unscaledDeltaTime;
                        elapsed += dt;
                        if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) continue;
                        frameTimes.Add(dt * 1000f);
                        Record(cpu, cpuTimes, 0.000001f);
                        Record(gpu, gpuTimes, 0.000001f);
                        Record(draw, draws, 1f);
                        Record(batchCounter, batches, 1f);
                    }
                    if (cpu.Valid) cpu.Dispose();
                    if (gpu.Valid) gpu.Dispose();
                    if (draw.Valid) draw.Dispose();
                    if (batchCounter.Valid) batchCounter.Dispose();
                    long? allocatedAfter = ThreadAllocated();
                    int gcCollections = GC.CollectionCount(0) - gcBefore;
                    if (captureFailure != null)
                    {
                        WriteUnavailable(target, repeat, mode, "failed", captureFailure);
                        Debug.LogError($"E2c.2 capture failed: V{scenarioIndex + 1}, repeat={repeat}, " +
                            $"mode={mode}, reason={captureFailure}, csv={outputPath}", this);
                        WriteRemaining(scenarioIndex, repeat, layout, mode, captureFailure);
                        yield return Cleanup();
                        yield break;
                    }
                    bool captured = WriteCapture(target, repeat, mode, activeRenderers, visibleRenderers,
                        frameTimes, cpuTimes, gpuTimes, draws, batches, gcCollections,
                        allocatedBefore.HasValue && allocatedAfter.HasValue
                            ? allocatedAfter.Value - allocatedBefore.Value : (long?)null);
                    if (!captured)
                    {
                        Debug.LogError($"E2c.2 capture failed: V{scenarioIndex + 1}, repeat={repeat}, " +
                            $"mode={mode}, no valid frame samples, csv={outputPath}", this);
                        WriteRemaining(scenarioIndex, repeat, layout, mode, "No valid frame samples.");
                        yield return Cleanup();
                        yield break;
                    }
                    Debug.Log($"E2c.2 capture complete: V{scenarioIndex + 1}, repeat={repeat}, " +
                        $"layout={layout}, camera={mode}, frames={frameTimes.Count}, csv={outputPath}", this);
                    yield return null;
                    }
                }
                Debug.Log($"E2c.2 scenario complete: V{scenarioIndex + 1}, pieces={world.Count}, views={views.Count}", this);
            }
            Debug.Log($"E2c.2 visual benchmark complete: csv={outputPath}", this);
            yield return Cleanup();
        }

        private static ProfilerRecorder StartCounter(ProfilerCategory category, string name)
        {
            try { return ProfilerRecorder.StartNew(category, name, 16); }
            catch { return default; }
        }

        private static void Record(ProfilerRecorder recorder, List<float> samples, float multiplier)
        {
            if (recorder.Valid && recorder.LastValue > 0)
                samples.Add(recorder.LastValue * multiplier);
        }

        private static Func<long> FindThreadAllocationCounter()
        {
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            if (method == null) return null;
            try { return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method); }
            catch { return null; }
        }

        private static long? ThreadAllocated()
        {
            try { return ThreadAllocationCounter?.Invoke(); }
            catch { return null; }
        }

        private IEnumerator PrepareLayout(string layout)
        {
            layoutFailure = null;
            chunkBuildMilliseconds = 0d;
            if (chunks != null) { chunks.Dispose(); chunks = null; yield return null; }
            if (layout == "baseline") yield break;
            chunks = new VisualChunkManager(visualChunkSize, cullingReleaseSeconds, transform);
            Stopwatch wall = Stopwatch.StartNew();
            for (int i = 0; i < views.Count; i += buildBatchSize)
            {
                if (VisualBenchmarkRules.ExceedsGenerationSeconds(wall.Elapsed.TotalSeconds, maxGenerationSeconds) ||
                    VisualBenchmarkRules.ExceedsMemory(GC.GetTotalMemory(false), Profiler.GetTotalReservedMemoryLong(),
                        maxUnityReservedMB * 1048576L))
                { layoutFailure = "Chunk setup exceeded time or memory safety limit."; break; }
                Stopwatch batch = Stopwatch.StartNew();
                try
                {
                    for (int j = i; j < Math.Min(i + buildBatchSize, views.Count); j++)
                    {
                        if (!world.TryGet(views[j].Id, out PieceData piece))
                            throw new InvalidOperationException("View has no logical PieceData.");
                        chunks.Register(piece, views[j]);
                    }
                }
                catch (Exception error) { layoutFailure = error.GetType().Name + ": " + error.Message; }
                batch.Stop();
                chunkBuildMilliseconds += batch.Elapsed.TotalMilliseconds;
                if (layoutFailure != null) break;
                yield return null;
            }
            if (layoutFailure == null) chunks.SetCulling(layout == "chunked_culling");
            Debug.Log($"E2c.2 layout prepared: {layout}, chunks={chunks.ChunkCount}, " +
                $"pieces={chunks.PieceCount}, buildMs={chunkBuildMilliseconds:F1}, failure={layoutFailure ?? "none"}", this);
        }

        private void PositionCamera(string mode, float progress)
        {
            float angle = mode == "path" ? progress * Mathf.PI * 2f : 0f;
            Vector3 position = mode == "overview" ? new Vector3(0f, 1200f, -900f)
                : new Vector3(Mathf.Sin(angle) * 240f, 160f, -Mathf.Cos(angle) * 240f);
            benchmarkCamera.transform.position = position;
            benchmarkCamera.transform.LookAt(Vector3.zero);
        }

        private bool WriteCapture(int target, int repeat, string mode, int active, int visible,
            List<float> frames, List<float> cpu, List<float> gpu, List<float> draws, List<float> batches,
            int gcCollections, long? threadAllocation)
        {
            if (frames.Count == 0) { WriteUnavailable(target, repeat, mode, "failed", "No valid frame samples."); return false; }
            frames.Sort(); cpu.Sort(); gpu.Sort(); draws.Sort(); batches.Sort();
            double total = 0d;
            foreach (float frame in frames) total += frame;
            double meanFrame = total / frames.Count;
            string N(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
            string Metric(List<float> values, float divisor = 1f)
            {
                if (values.Count == 0) return "unavailable";
                double sum = 0d;
                foreach (float value in values) sum += value;
                return N(sum / values.Count / divisor);
            }
            Append(BaseFields(target, repeat, mode, active, visible, "ok", "",
                N(cumulativeCreateMilliseconds), N(warmupSeconds), N(captureSeconds), frames.Count.ToString(),
                N(1000d / meanFrame), N(1000d / VisualBenchmarkRules.Percentile(frames.ToArray(), 0.5d)),
                N(1000d / VisualBenchmarkRules.Percentile(frames.ToArray(), 0.99d)),
                N(meanFrame), N(VisualBenchmarkRules.Percentile(frames.ToArray(), 0.5d)),
                N(VisualBenchmarkRules.Percentile(frames.ToArray(), 0.95d)),
                N(VisualBenchmarkRules.Percentile(frames.ToArray(), 0.99d)),
                Metric(cpu), Metric(gpu), Metric(draws), Metric(batches),
                N(GC.GetTotalMemory(false) / 1048576d),
                N(Profiler.GetTotalAllocatedMemoryLong() / 1048576d),
                N(Profiler.GetTotalReservedMemoryLong() / 1048576d), gcCollections.ToString(),
                threadAllocation.HasValue ? threadAllocation.Value.ToString() : "unavailable"));
            return true;
        }

        private void WriteUnavailable(int target, int repeat, string mode, string status, string reason) =>
            Append(BaseFields(target, repeat, mode, -1, -1, status, reason));

        private void WriteRemaining(int scenarioIndex, int currentRepeat, string currentLayout,
            string currentCamera, string reason)
        {
            for (int scenario = scenarioIndex; scenario < VisualBenchmarkScenario.Sizes.Length; scenario++)
                for (int repeat = 1; repeat <= 3; repeat++)
                    foreach (string layout in VisualChunkBenchmarkModes.Layouts(repeat))
                    foreach (string cameraMode in VisualChunkBenchmarkModes.Cameras)
                    {
                        bool sameScenario = scenario == scenarioIndex;
                        if (sameScenario && repeat < currentRepeat) continue;
                        int layoutSlot = Array.IndexOf(VisualChunkBenchmarkModes.Layouts(repeat), layout);
                        int failedLayoutSlot = Array.IndexOf(VisualChunkBenchmarkModes.Layouts(repeat), currentLayout);
                        if (sameScenario && repeat == currentRepeat && layoutSlot < failedLayoutSlot) continue;
                        bool sameLayout = sameScenario && repeat == currentRepeat && layout == currentLayout;
                        if (sameLayout && currentCamera != null &&
                            Array.IndexOf(VisualChunkBenchmarkModes.Cameras, cameraMode) <=
                            Array.IndexOf(VisualChunkBenchmarkModes.Cameras, currentCamera)) continue;
                        layoutMode = layout;
                        WriteUnavailable(VisualBenchmarkScenario.Sizes[scenario], repeat, cameraMode,
                            sameLayout && currentCamera == null ? "failed" : "skipped", reason);
                    }
        }

        private string[] ChunkFields(string[] original)
        {
            var fields = new List<string>(original.Length + 10);
            bool validCapture = original[original.Length - 2] == "ok";
            for (int i = 0; i < original.Length - 2; i++) fields.Add(original[i]);
            fields.Add(layoutMode);
            fields.Add(views.Count.ToString());
            fields.Add(validCapture ? snapshotChunksExisting.ToString() : chunks != null ? chunks.ChunkCount.ToString() : "0");
            fields.Add(validCapture ? snapshotChunksActive.ToString() : "unavailable");
            fields.Add(validCapture && layoutMode == "chunked_culling" ? snapshotChunksVisible.ToString() : "unavailable");
            fields.Add(validCapture ? snapshotChunksDiscarded.ToString() : chunks != null ? chunks.DiscardedChunks.ToString() : "0");
            fields.Add(chunkBuildMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
            fields.Add(validCapture && visibilitySamples > 0
                ? (visibilityMilliseconds / visibilitySamples).ToString("F3", CultureInfo.InvariantCulture)
                : "unavailable");
            fields.Add(validCapture ? visibilitySamples.ToString() : "unavailable");
            fields.Add(validCapture && chunks != null ? (chunks.StateChanges - stateChangesBefore).ToString() : "unavailable");
            fields.Add(original[original.Length - 2]); fields.Add(original[original.Length - 1]);
            return fields.ToArray();
        }

        private string[] BaseFields(int target, int repeat, string mode, int active, int visible,
            string status, string error, params string[] measurements)
        {
            string scenario = target == 0 ? "setup" : "V" + (Array.IndexOf(VisualBenchmarkScenario.Sizes, target) + 1);
            var fields = new List<string> {
                DateTime.UtcNow.ToString("o"), Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable",
                Application.unityVersion, Application.platform.ToString(), Application.isEditor ? "Editor" : "Player",
                SystemInfo.processorType, SystemInfo.graphicsDeviceName,
                SystemInfo.systemMemorySize > 0 ? SystemInfo.systemMemorySize.ToString() : "unavailable",
                SystemInfo.graphicsMemorySize > 0 ? SystemInfo.graphicsMemorySize.ToString() : "unavailable",
                scenario, seed.ToString(), repeat.ToString(), mode, world != null ? world.Count.ToString() : "0",
                views.Count.ToString(), active >= 0 ? active.ToString() : "unavailable",
                visible >= 0 ? visible.ToString() : "unavailable", Screen.width.ToString(),
                Screen.height.ToString(), QualitySettings.vSyncCount.ToString(), Application.targetFrameRate.ToString(),
                QualitySettings.GetQualityLevel().ToString()
            };
            for (int i = 0; i < 20; i++) fields.Add(i < measurements.Length ? measurements[i] : "unavailable");
            fields.Add(status); fields.Add(error);
            return fields.ToArray();
        }

        private void Append(string[] fields) => File.AppendAllText(outputPath,
            VisualChunkCsv.Row(ChunkFields(fields)) + Environment.NewLine);

        private IEnumerator Cleanup()
        {
            if (chunks != null) { chunks.Dispose(); chunks = null; }
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] != null) Destroy(views[i].gameObject);
                if (i % 128 == 127) yield return null;
            }
            views.Clear();
            world = null;
        }
    }
}
