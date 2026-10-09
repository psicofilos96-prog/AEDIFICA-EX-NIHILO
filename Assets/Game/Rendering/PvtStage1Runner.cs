using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Aedifica.Construction;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Aedifica.Rendering
{
    // PVT-1 only: paired old/new visual representations over identical logical PieceData.
    public sealed class PvtStage1Runner : MonoBehaviour
    {
        private static readonly string[] CameraModes = { "fixed", "path", "overview", "street", "construction_near" };
        [SerializeField] private Camera benchmarkCamera;
        [SerializeField] private Material neutralMaterial, stoneMaterial, brickMaterial, plasterMaterial;
        [SerializeField] private int seed = PvtScenario.Seed;
        [SerializeField] private int batchSize = 32;
        [SerializeField] private int maxReservedMB = 4096;
        [SerializeField] private float maxSetupSeconds = 1200f;
        [SerializeField] private float warmupSeconds = 15f;
        [SerializeField] private float captureSeconds = 30f;
        [SerializeField] private float maxCaptureWallSeconds = 120f;

        private readonly Dictionary<PieceId, PieceView> views = new Dictionary<PieceId, PieceView>();
        private readonly HashSet<string> frameRows = new HashSet<string>();
        private readonly HashSet<string> editRows = new HashSet<string>();
        private ConstructionWorld world;
        private MaterialRegistry materials;
        private PvtChunkVisualEngine combined;
        private GameObject visualRoot;
        private string framesPath, editsPath, layout, setupError, captureError, editError;
        private double logicalCreateMs, visualBuildMs;

        public void Configure(Camera camera, Material neutral, Material stone, Material brick, Material plaster)
        {
            benchmarkCamera = camera; neutralMaterial = neutral; stoneMaterial = stone;
            brickMaterial = brick; plasterMaterial = plaster;
        }

        private IEnumerator Start()
        {
            string prefix = $"aedifica_PVT1_{seed}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}";
            Directory.CreateDirectory(Application.persistentDataPath);
            framesPath = Path.Combine(Application.persistentDataPath, prefix + "_frames.csv");
            editsPath = Path.Combine(Application.persistentDataPath, prefix + "_edits.csv");
            File.WriteAllText(framesPath, PvtBenchmarkCsv.FramesHeader + Environment.NewLine);
            File.WriteAllText(editsPath, PvtBenchmarkCsv.EditsHeader + Environment.NewLine);
            if (benchmarkCamera == null || neutralMaterial == null || batchSize < 1 || maxReservedMB < 1 ||
                maxSetupSeconds <= 0f || warmupSeconds < 0f || captureSeconds <= 0f ||
                maxCaptureWallSeconds < captureSeconds)
            {
                WriteFrame(0, 0, "setup", "setup", "failed", "Invalid benchmark configuration.", null);
                Debug.LogError($"PVT-1 setup failed; CSV={framesPath}", this);
                yield break;
            }
            if (!Application.isEditor) Screen.SetResolution(1920, 1080, false);
            world = new ConstructionWorld();
            materials = new MaterialRegistry(neutralMaterial, stoneMaterial, brickMaterial, plasterMaterial);
            Debug.Log($"PVT-1 begin: seed={seed}, commit={Commit}, resolution={Screen.width}x{Screen.height}, " +
                $"vsync={QualitySettings.vSyncCount}, frameCap={Application.targetFrameRate}, " +
                $"frames={framesPath}, edits={editsPath}", this);

            for (int scenario = 0; scenario < PvtScenario.Sizes.Length; scenario++)
            {
                int target = PvtScenario.Sizes[scenario];
                Debug.Log($"PVT-1 scenario {Name(target)} generation begin, target={target}", this);
                Stopwatch generation = Stopwatch.StartNew();
                while (world.Count < target)
                {
                    if (SafetyFailure(generation, out setupError)) break;
                    int end = Math.Min(target, world.Count + batchSize);
                    Stopwatch batch = Stopwatch.StartNew();
                    try
                    {
                        while (world.Count < end)
                            if (!world.Create(PvtScenario.PieceAt(world.Count, seed)).Changed)
                                throw new InvalidOperationException("Duplicate scenario ID.");
                    }
                    catch (Exception error) { setupError = error.GetType().Name + ": " + error.Message; }
                    batch.Stop(); logicalCreateMs += batch.Elapsed.TotalMilliseconds;
                    if (setupError != null) break;
                    if (world.Count % 1000 < batchSize)
                        Debug.Log($"PVT-1 logical progress: {world.Count}/{target}, reservedMB={ReservedMB()}", this);
                    yield return null;
                }
                if (setupError != null)
                {
                    Debug.LogError($"PVT-1 generation failed at {world.Count}/{target}: {setupError}", this);
                    WritePending(scenario, 1, "baseline", null, setupError, true);
                    yield return ClearVisuals();
                    yield break;
                }

                for (int repeat = 1; repeat <= 3; repeat++)
                {
                    string[] order = (repeat + scenario) % 2 == 0
                        ? new[] { "baseline", "combined" } : new[] { "combined", "baseline" };
                    foreach (string mode in order)
                    {
                        layout = mode;
                        yield return Prepare(mode);
                        if (setupError != null)
                        {
                            Debug.LogError($"PVT-1 visual setup failed: {Name(target)} {mode}: {setupError}", this);
                            WritePending(scenario, repeat, mode, null, setupError, true);
                            yield return ClearVisuals();
                            yield break;
                        }
                        foreach (string cameraMode in CameraModes)
                        {
                            yield return Capture(target, repeat, cameraMode);
                            if (captureError != null)
                            {
                                WritePending(scenario, repeat, mode, cameraMode, captureError, false);
                                yield return ClearVisuals();
                                yield break;
                            }
                        }
                        yield return EditProbe(target, repeat);
                        if (editError != null)
                        {
                            Debug.LogError($"PVT-1 edit integrity failed: {Name(target)} {mode}: {editError}", this);
                            WritePending(scenario, repeat, mode, "construction_near", editError, false);
                            yield return ClearVisuals();
                            yield break;
                        }
                        yield return ClearVisuals();
                    }
                }
                Debug.Log($"PVT-1 scenario {Name(target)} complete; logical={world.Count}", this);
            }
            Debug.Log($"PVT-1 complete: frames={framesPath}, edits={editsPath}", this);
        }

        private bool SafetyFailure(Stopwatch timer, out string error)
        {
            error = null;
            if (timer.Elapsed.TotalSeconds > maxSetupSeconds) error = $"Setup exceeded {maxSetupSeconds:F0}s.";
            else if (VisualBenchmarkRules.ExceedsMemory(GC.GetTotalMemory(false),
                Profiler.GetTotalReservedMemoryLong(), maxReservedMB * 1048576L))
                error = $"Reserved/managed memory exceeded {maxReservedMB} MB.";
            return error != null;
        }

        private IEnumerator Prepare(string mode)
        {
            setupError = null; visualBuildMs = 0d;
            yield return ClearVisuals();
            visualRoot = new GameObject("PVT-1 " + mode);
            visualRoot.transform.SetParent(transform, false);
            Stopwatch wall = Stopwatch.StartNew();
            if (mode == "combined")
            {
                try { combined = new PvtChunkVisualEngine(world, materials, visualRoot.transform); }
                catch (Exception error) { setupError = error.GetType().Name + ": " + error.Message; }
                if (setupError == null)
                {
                    while (combined.DirtyRegionCount > 0)
                    {
                        if (SafetyFailure(wall, out setupError)) break;
                        Stopwatch build = Stopwatch.StartNew();
                        try { combined.RebuildDirty(1); }
                        catch (Exception error) { setupError = error.GetType().Name + ": " + error.Message; }
                        build.Stop(); visualBuildMs += build.Elapsed.TotalMilliseconds;
                        if (setupError != null) break;
                        yield return null;
                    }
                }
            }
            else
            {
                for (int i = 0; i < world.Count; i += batchSize)
                {
                    if (SafetyFailure(wall, out setupError)) break;
                    Stopwatch batch = Stopwatch.StartNew();
                    try
                    {
                        for (int j = i; j < Math.Min(world.Count, i + batchSize); j++)
                        {
                            PieceData expected = PvtScenario.PieceAt(j, seed);
                            if (!world.TryGet(expected.Id, out PieceData piece))
                                throw new InvalidOperationException("Missing logical piece during visual build.");
                            CreateView(piece);
                        }
                    }
                    catch (Exception error) { setupError = error.GetType().Name + ": " + error.Message; }
                    batch.Stop(); visualBuildMs += batch.Elapsed.TotalMilliseconds;
                    if (setupError != null) break;
                    yield return null;
                }
            }
            Debug.Log($"PVT-1 visual prepared: {mode}, pieces={world.Count}, views={views.Count}, " +
                $"pages={combined?.PageCount ?? 0}, buildMs={visualBuildMs:F1}, error={setupError ?? "none"}", this);
        }

        private void CreateView(PieceData piece)
        {
            var go = new GameObject($"PVT baseline {piece.Id}");
            go.transform.SetParent(visualRoot.transform, false);
            try
            {
                PieceView view = go.AddComponent<PieceView>();
                view.Initialize(piece, materials);
                views.Add(piece.Id, view);
            }
            catch { Destroy(go); throw; }
        }

        private IEnumerator ClearVisuals()
        {
            if (combined != null) { combined.Dispose(); combined = null; }
            if (visualRoot != null) { Destroy(visualRoot); visualRoot = null; }
            views.Clear();
            yield return null; // allow Unity to release old objects/meshes before the next mode
        }

        private void PositionCamera(string cameraMode, float progress)
        {
            float angle = cameraMode == "path" ? progress * Mathf.PI * 2f : 0f;
            Vector3 position = cameraMode == "overview" ? new Vector3(0f, 1200f, -900f)
                : cameraMode == "street" ? new Vector3(0f, 2.5f, -30f)
                : cameraMode == "construction_near" ? new Vector3(0f, 15f, -35f)
                : new Vector3(Mathf.Sin(angle) * 240f, 160f, -Mathf.Cos(angle) * 240f);
            benchmarkCamera.transform.position = position;
            benchmarkCamera.transform.LookAt(cameraMode == "street" || cameraMode == "construction_near"
                ? new Vector3(0f, 2f, 0f) : Vector3.zero);
        }

        private IEnumerator Capture(int target, int repeat, string cameraMode)
        {
            captureError = null;
            PositionCamera(cameraMode, 0f);
            float warmup = 0f;
            while (warmup < warmupSeconds)
            {
                if (cameraMode == "path") PositionCamera(cameraMode, warmupSeconds > 0f ? warmup / warmupSeconds : 0f);
                yield return null;
                warmup += Time.unscaledDeltaTime;
            }
            PositionCamera(cameraMode, 0f);
            yield return null;
            int representation = layout == "combined" ? combined.PageCount : views.Count;
            int visible = layout == "combined" ? combined.VisibleRendererCount() : CountVisibleViews();
            int triangles = layout == "combined" ? combined.TriangleCount : CountBaselineTriangles();
            var frameTimes = new List<float>(8192);
            var counters = new PvtFrameCounters();
            yield return null; // exclude visibility enumeration and recorder setup
            float elapsed = 0f;
            Stopwatch wall = Stopwatch.StartNew();
            while (elapsed < captureSeconds)
            {
                if (wall.Elapsed.TotalSeconds > maxCaptureWallSeconds)
                {
                    captureError = "Capture exceeded wall-time safety limit.";
                    break;
                }
                if (cameraMode == "path") PositionCamera(cameraMode, elapsed / captureSeconds);
                yield return null;
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) continue;
                frameTimes.Add(dt * 1000f);
                counters.Read();
            }
            counters.Dispose();
            if (frameTimes.Count == 0) captureError = "No valid frame samples.";
            WriteFrame(target, repeat, layout, cameraMode, captureError == null ? "ok" : "failed",
                captureError ?? "", new FrameSample(frameTimes, counters, representation, visible, triangles));
            Debug.Log($"PVT-1 capture {Name(target)} {layout} {cameraMode} repeat={repeat}, " +
                $"frames={frameTimes.Count}, status={(captureError == null ? "ok" : "failed")}, csv={framesPath}", this);
        }

        private int CountVisibleViews()
        {
            int count = 0;
            foreach (PieceView view in views.Values)
                if (view.GetComponent<MeshRenderer>().isVisible) count++;
            return count;
        }

        private int CountBaselineTriangles()
        {
            long count = 0;
            foreach (PieceView view in views.Values)
                count += (long)view.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3L;
            if (count > int.MaxValue) throw new InvalidOperationException("Triangle count exceeds reportable range.");
            return (int)count;
        }

        private sealed class FrameSample
        {
            public readonly List<float> Frames;
            public readonly PvtFrameCounters Counters;
            public readonly int Representation, Visible, Triangles;
            public FrameSample(List<float> frames, PvtFrameCounters counters, int representation, int visible, int triangles)
            { Frames = frames; Counters = counters; Representation = representation; Visible = visible; Triangles = triangles; }
        }

        private void WriteFrame(int target, int repeat, string mode, string cameraMode,
            string status, string error, FrameSample sample)
        {
            string U = "unavailable", N(double value) => PvtBenchmarkCsv.Number(value);
            List<float> frames = sample?.Frames;
            double sum = 0d, maximum = 0d;
            int above33 = 0, above50 = 0;
            if (frames != null)
            {
                foreach (float value in frames)
                {
                    sum += value; maximum = Math.Max(maximum, value);
                    if (value > 33.3f) above33++;
                    if (value > 50f) above50++;
                }
                frames.Sort();
            }
            double mean = frames != null && frames.Count > 0 ? sum / frames.Count : double.NaN;
            long? rss = ProcessRss();
            string[] fields = {
                DateTime.UtcNow.ToString("o"), Commit, Application.unityVersion, Application.platform.ToString(),
                Application.isEditor ? "Editor" : Debug.isDebugBuild ? "DevelopmentPlayer" : "ReleasePlayer",
                SystemInfo.processorType, SystemInfo.graphicsDeviceName,
                SystemInfo.systemMemorySize > 0 ? SystemInfo.systemMemorySize.ToString() : U,
                SystemInfo.graphicsMemorySize > 0 ? SystemInfo.graphicsMemorySize.ToString() : U,
                Name(target), seed.ToString(), mode, cameraMode, repeat.ToString(),
                world != null ? world.Count.ToString() : "0",
                sample != null ? (sample.Representation + 1).ToString() : U,
                sample != null ? sample.Representation.ToString() : U,
                sample != null ? sample.Visible.ToString() : U,
                sample != null ? sample.Representation.ToString() : U,
                sample != null ? sample.Representation.ToString() : U,
                sample != null ? sample.Triangles.ToString() : U,
                sample != null ? (combined?.RegionCount ?? 0).ToString() : U,
                Screen.width.ToString(), Screen.height.ToString(), QualitySettings.GetQualityLevel().ToString(),
                QualitySettings.vSyncCount.ToString(), Application.targetFrameRate.ToString(),
                frames != null ? frames.Count.ToString() : U,
                N(1000d / mean), N(1000d / PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.99d)),
                N(1000d / PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.95d)),
                N(1000d / PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.5d)),
                N(mean), N(PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.5d)),
                N(PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.95d)),
                N(PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.99d)),
                frames != null && frames.Count > 0 ? N(maximum) : U,
                frames != null ? above33.ToString() : U, frames != null ? above50.ToString() : U,
                sample != null ? PvtBenchmarkCsv.Mean(sample.Counters.Main) : U,
                sample != null ? PvtBenchmarkCsv.Mean(sample.Counters.Render) : U,
                sample != null ? PvtBenchmarkCsv.Mean(sample.Counters.Gpu) : U,
                sample != null ? PvtBenchmarkCsv.Mean(sample.Counters.Draws) : U,
                sample != null ? PvtBenchmarkCsv.Mean(sample.Counters.Batches) : U,
                sample != null ? PvtBenchmarkCsv.Mean(sample.Counters.SetPass) : U,
                N(GC.GetTotalMemory(false) / 1048576d), N(Profiler.GetTotalReservedMemoryLong() / 1048576d),
                rss.HasValue ? N(rss.Value / 1048576d) : U, U,
                N(logicalCreateMs), N(visualBuildMs), status, error
            };
            File.AppendAllText(framesPath, PvtBenchmarkCsv.Row(PvtBenchmarkCsv.FramesHeader, fields) + Environment.NewLine);
            if (target > 0) frameRows.Add(Key(target, repeat, mode, cameraMode));
        }

        private static long? ProcessRss()
        {
            try { using (Process process = Process.GetCurrentProcess()) return process.WorkingSet64; }
            catch { return null; }
        }

        private string Commit => Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable";
        private static string Name(int size) => size == 10000 ? "A" : size == 25000 ? "B" : size == 50000 ? "C" : "setup";
        private static string Key(int target, int repeat, string mode, string camera) =>
            target + ":" + repeat + ":" + mode + ":" + camera;
        private static long ReservedMB() => Profiler.GetTotalReservedMemoryLong() / 1048576L;

        private void WritePending(int failedScenario, int failedRepeat, string failedMode, string failedCamera,
            string reason, bool setupFailure)
        {
            for (int scenario = failedScenario; scenario < PvtScenario.Sizes.Length; scenario++)
            for (int repeat = 1; repeat <= 3; repeat++)
            foreach (string mode in new[] { "baseline", "combined" })
            foreach (string camera in CameraModes)
            {
                int target = PvtScenario.Sizes[scenario];
                if (frameRows.Contains(Key(target, repeat, mode, camera))) continue;
                string status = setupFailure && scenario == failedScenario && repeat == failedRepeat && mode == failedMode &&
                    (failedCamera == null || failedCamera == camera) ? "failed" : "skipped";
                WriteFrame(target, repeat, mode, camera, status, reason, null);
            }
            for (int scenario = failedScenario; scenario < PvtScenario.Sizes.Length; scenario++)
            for (int repeat = 1; repeat <= 3; repeat++)
            foreach (string mode in new[] { "baseline", "combined" })
            {
                int target = PvtScenario.Sizes[scenario];
                string key = target + ":" + repeat + ":" + mode;
                if (editRows.Contains(key)) continue;
                string status = setupFailure && scenario == failedScenario && repeat == failedRepeat && mode == failedMode
                    ? "failed" : "skipped";
                foreach (string operation in new[] { "select", "begin_edit", "move_rebuild", "undo_move_rebuild", "resize_rebuild",
                    "undo_resize_rebuild", "delete_rebuild", "undo_delete_rebuild",
                    "redo_delete_rebuild", "undo_redo_rebuild", "end_edit" })
                {
                    string[] fields = { DateTime.UtcNow.ToString("o"), Commit, Name(target), seed.ToString(),
                        mode, repeat.ToString(), operation, "unavailable", "unavailable", "unavailable",
                        "unavailable", "unavailable", "unavailable", status, reason };
                    File.AppendAllText(editsPath, PvtBenchmarkCsv.Row(PvtBenchmarkCsv.EditsHeader, fields) + Environment.NewLine);
                }
                editRows.Add(key);
            }
        }

        private IEnumerator EditProbe(int target, int repeat)
        {
            editError = null;
            var before = PvtIntegrity.Capture(world);
            var operations = new Dictionary<string, List<float>>();
            string[] names = { "select", "begin_edit", "move_rebuild", "undo_move_rebuild", "resize_rebuild",
                "undo_resize_rebuild", "delete_rebuild", "undo_delete_rebuild",
                "redo_delete_rebuild", "undo_redo_rebuild", "end_edit" };
            foreach (string name in names) operations.Add(name, new List<float>());
            var history = new ConstructionCommandHistory(world);
            PieceData original = PvtScenario.PieceAt(5, seed); // roof is the visible ray target
            if (!world.TryGet(original.Id, out original)) editError = "Missing edit target.";
            for (int cycle = 0; cycle < 8 && editError == null; cycle++)
            {
                try
                {
                    Physics.SyncTransforms();
                    Stopwatch timer = Stopwatch.StartNew();
                    bool selected = TrySelect(original, out PieceId selectedId);
                    timer.Stop(); operations["select"].Add((float)timer.Elapsed.TotalMilliseconds);
                    if (!selected || selectedId != original.Id || !world.TryGet(selectedId, out _))
                        throw new InvalidOperationException("Physics picking did not resolve the intended live PieceId.");
                    MeasureAction("begin_edit", () =>
                    {
                        if (layout == "combined") combined.BeginEdit(original.Id);
                        else views[original.Id].SetSelected(true);
                    }, operations);

                    PieceData moved = original.WithTransform(new PieceTransform(
                        original.Transform.Position + new Vector3(0.25f, 0f, 0f), original.Transform.Rotation));
                    Measure("move_rebuild", () => history.Update(original.Id, moved), operations);
                    Measure("undo_move_rebuild", () => Undo(history), operations);
                    PieceData resized = original.WithDimensions(original.Dimensions.Resize(0, original.Dimensions.X + 0.25f));
                    Measure("resize_rebuild", () => history.Update(original.Id, resized), operations);
                    Measure("undo_resize_rebuild", () => Undo(history), operations);
                    Measure("delete_rebuild", () => history.Delete(original.Id), operations);
                    Measure("undo_delete_rebuild", () => Undo(history), operations);
                    Measure("redo_delete_rebuild", () => Redo(history), operations);
                    Measure("undo_redo_rebuild", () => Undo(history), operations);
                    MeasureAction("end_edit", () =>
                    {
                        if (layout == "combined") combined.EndEdit(original.Id);
                        else views[original.Id].SetSelected(false);
                    }, operations);
                }
                catch (Exception error) { editError = error.GetType().Name + ": " + error.Message; }
                yield return null;
            }
            bool integrity = editError == null && PvtIntegrity.Matches(world, before) &&
                (layout == "combined" ? combined.PieceCount == world.Count && combined.DirtyRegionCount == 0
                    : views.Count == world.Count);
            if (!integrity && editError == null) editError = "Logical/visual integrity mismatch after editing.";
            foreach (string name in names)
            {
                List<float> samples = operations[name];
                samples.Sort();
                string[] fields = {
                    DateTime.UtcNow.ToString("o"), Commit, Name(target), seed.ToString(), layout, repeat.ToString(), name,
                    samples.Count.ToString(), PvtBenchmarkCsv.Number(PvtBenchmarkCsv.Percentile(samples, 0.5d)),
                    PvtBenchmarkCsv.Number(PvtBenchmarkCsv.Percentile(samples, 0.95d)),
                    PvtBenchmarkCsv.Number(PvtBenchmarkCsv.Percentile(samples, 0.99d)),
                    samples.Count > 0 ? PvtBenchmarkCsv.Number(samples[samples.Count - 1]) : "unavailable",
                    integrity ? "pass" : "fail", editError == null ? "ok" : "failed", editError ?? ""
                };
                File.AppendAllText(editsPath, PvtBenchmarkCsv.Row(PvtBenchmarkCsv.EditsHeader, fields) + Environment.NewLine);
            }
            editRows.Add(target + ":" + repeat + ":" + layout);
            Debug.Log($"PVT-1 edits {Name(target)} {layout} repeat={repeat}, cycles=8, " +
                $"integrity={(integrity ? "pass" : "fail")}, csv={editsPath}", this);
        }

        private bool TrySelect(PieceData target, out PieceId id)
        {
            Ray ray = new Ray(target.Transform.Position + new Vector3(0f, 30f, 0f), Vector3.down);
            if (layout == "combined") return combined.TryPick(ray, 60f, out id);
            RaycastHit[] hits = Physics.RaycastAll(ray, 60f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                PieceView view = hit.collider.GetComponent<PieceView>();
                if (view == null || !views.ContainsKey(view.Id)) continue;
                id = view.Id;
                return true;
            }
            id = default;
            return false;
        }

        private static ConstructionChangeSet Undo(ConstructionCommandHistory history)
        {
            if (!history.TryUndo(out ConstructionChangeSet change))
                throw new InvalidOperationException("Undo failed.");
            return change;
        }

        private static ConstructionChangeSet Redo(ConstructionCommandHistory history)
        {
            if (!history.TryRedo(out ConstructionChangeSet change))
                throw new InvalidOperationException("Redo failed.");
            return change;
        }

        private void Measure(string operation, Func<ConstructionChangeSet> mutate,
            Dictionary<string, List<float>> samples)
        {
            Stopwatch timer = Stopwatch.StartNew();
            ConstructionChangeSet change = mutate();
            if (!change.Changed) throw new InvalidOperationException(operation + " did not change the world.");
            if (layout == "combined") combined.RebuildDirty();
            else SyncView(change);
            timer.Stop();
            samples[operation].Add((float)timer.Elapsed.TotalMilliseconds);
        }

        private static void MeasureAction(string operation, Action action,
            Dictionary<string, List<float>> samples)
        {
            Stopwatch timer = Stopwatch.StartNew();
            action();
            timer.Stop();
            samples[operation].Add((float)timer.Elapsed.TotalMilliseconds);
        }

        private void SyncView(ConstructionChangeSet change)
        {
            if (change.After == null)
            {
                if (views.TryGetValue(change.PieceId, out PieceView removed))
                {
                    views.Remove(change.PieceId);
                    if (removed != null) Destroy(removed.gameObject);
                }
                return;
            }
            if (views.TryGetValue(change.PieceId, out PieceView existing)) existing.Refresh(change.After);
            else CreateView(change.After);
        }

        private void OnDestroy()
        {
            if (combined != null) combined.Dispose();
        }
    }
}
