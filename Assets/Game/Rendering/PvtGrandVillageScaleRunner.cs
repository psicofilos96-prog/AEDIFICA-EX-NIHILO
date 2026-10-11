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
    // A separate Windows Player workload: never runs when reviewing the authored village.
    public sealed class PvtGrandVillageScaleRunner : MonoBehaviour
    {
        public const string Header = "utc,commit,unity,environment,cpu,gpu,ram_mb,vram_mb,scenario,pieces,tiles,repeat,camera,seed,screen_width,screen_height,vsync,target_fps,build_logical_ms,build_environment_ms,build_visual_ms,regions,pages,environment_renderers,vertices,triangles,frames,fps_mean,fps_p01,frame_p50_ms,frame_p95_ms,frame_p99_ms,frame_max_ms,cpu_main_ms,cpu_render_ms,gpu_ms,draw_calls,batches,setpass,managed_mb,unity_reserved_mb,process_rss_mb,vram_used_mb,status,error";
        public const string EditsHeader = "utc,commit,scenario,pieces,operation,milliseconds,rebuild_regions,status,error";
        private const int MemoryLimitMB = 8192;
        private const float WarmupSeconds = 20f;
        private const float CaptureSeconds = 45f;
        private const float WallLimitSeconds = 240f;
        private static readonly string[] Views = { "castle", "street", "panorama" };
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Material stone, brick, wood, plaster, roof, road, terrain, glass, water;
        [SerializeField] private Texture2D dryAlbedo, dryNormal, dryMask, groundAlbedo, groundNormal, groundMask;
        [SerializeField] private GameObject tree, pine, shrub, rock;
        private ConstructionWorld world;
        private PvtChunkVisualEngine engine;
        private readonly List<PvtGrandVillageEnvironment> environments = new List<PvtGrandVillageEnvironment>();
        private readonly List<GameObject> tileRoots = new List<GameObject>();
        private GameObject visualRoot;
        private string csvPath, editsPath;
        private double logicalMs, environmentMs, visualMs;
        private int environmentRenderers;

        public void Configure(Camera camera, Material stoneMat, Material brickMat, Material woodMat,
            Material plasterMat, Material roofMat, Material roadMat, Material terrainMat,
            Material glassMat, Material waterMat, Texture2D dryA, Texture2D dryN, Texture2D dryM,
            Texture2D groundA, Texture2D groundN, Texture2D groundM, GameObject treePrefab,
            GameObject pinePrefab, GameObject shrubPrefab, GameObject rockPrefab)
        {
            sceneCamera = camera; stone = stoneMat; brick = brickMat; wood = woodMat;
            plaster = plasterMat; roof = roofMat; road = roadMat; terrain = terrainMat;
            glass = glassMat; water = waterMat; dryAlbedo = dryA; dryNormal = dryN; dryMask = dryM;
            groundAlbedo = groundA; groundNormal = groundN; groundMask = groundM;
            tree = treePrefab; pine = pinePrefab; shrub = shrubPrefab; rock = rockPrefab;
        }

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-grand-scale-run") < 0)
            { Debug.Log("Grand scale lab is idle. Launch Player with -grand-scale-run.", this); yield break; }
            if (sceneCamera == null || stone == null || brick == null || wood == null ||
                plaster == null || roof == null || road == null || terrain == null || glass == null ||
                water == null || dryAlbedo == null || dryNormal == null || dryMask == null ||
                groundAlbedo == null || groundNormal == null || groundMask == null ||
                tree == null || pine == null || shrub == null || rock == null)
            { Debug.LogError("Grand scale lab has missing assets.", this); yield break; }
            if (!Application.isEditor)
            {
                Screen.SetResolution(1920, 1080, false);
                yield return null;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Directory.CreateDirectory(Application.persistentDataPath);
            csvPath = Path.Combine(Application.persistentDataPath,
                "aedifica_grand_scale_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".csv");
            editsPath = Path.ChangeExtension(csvPath, ".edits.csv");
            File.WriteAllText(csvPath, Header + Environment.NewLine);
            File.WriteAllText(editsPath, EditsHeader + Environment.NewLine);
            Debug.Log("Grand scale CSV: " + csvPath, this);
            List<PieceData> template = PvtGrandVillageScenario.Build();
            if (!Application.isEditor && (Screen.width != 1920 || Screen.height != 1080))
            {
                Write(1000, 0, "setup", template.Count, null, null, "failed",
                    "Player could not enter 1920x1080.");
                yield break;
            }
            foreach (int target in PvtGrandVillageScaleScenario.Sizes)
            {
                string error = null;
                yield return Prepare(target, template, result => error = result);
                if (error != null)
                {
                    Write(target, 0, "build", template.Count, null, null, "failed", error);
                    Debug.LogError("Grand scale stopped at " + target + ": " + error, this);
                    break;
                }
                try { EditProbe(target, template); }
                catch (Exception ex)
                {
                    error = "Edit probe: " + ex;
                    Write(target, 0, "edit", template.Count, null, null, "failed", error);
                    break;
                }
                for (int repeat = 1; repeat <= 3 && error == null; repeat++)
                    foreach (string view in Views)
                    {
                        yield return Capture(target, template.Count, repeat, view, result => error = result);
                        if (error != null) break;
                    }
                if (error != null) break;
            }
            Debug.Log("Grand scale run finished. CSV: " + csvPath, this);
        }

        private void EditProbe(int target, List<PieceData> template)
        {
            PieceData piece = null;
            foreach (PieceData candidate in template)
                if (candidate.Type == PieceType.Block) { piece = candidate; break; }
            if (piece == null) throw new InvalidOperationException("Village has no editable block.");
            var history = new ConstructionCommandHistory(world);
            int initialCount = world.Count;
            void Measure(string operation, Action action)
            {
                int before = engine.RebuiltRegions;
                Stopwatch timer = Stopwatch.StartNew();
                try
                {
                    action();
                    engine.RebuildDirty();
                    timer.Stop();
                    File.AppendAllText(editsPath, PvtBenchmarkCsv.Row(EditsHeader,
                        DateTime.UtcNow.ToString("o"), Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable",
                        "G" + target, world.Count.ToString(), operation,
                        PvtBenchmarkCsv.Number(timer.Elapsed.TotalMilliseconds),
                        (engine.RebuiltRegions - before).ToString(), "ok", "") + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    File.AppendAllText(editsPath, PvtBenchmarkCsv.Row(EditsHeader,
                        DateTime.UtcNow.ToString("o"), Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable",
                        "G" + target, world.Count.ToString(), operation,
                        PvtBenchmarkCsv.Number(timer.Elapsed.TotalMilliseconds),
                        (engine.RebuiltRegions - before).ToString(), "failed", ex.Message) + Environment.NewLine);
                    throw;
                }
            }
            PieceData moved = piece.WithTransform(new PieceTransform(
                piece.Transform.Position + new Vector3(0.3f, 0f, 0f), piece.Transform.Rotation));
            PieceData resized = moved.WithDimensions(moved.Dimensions.Resize(0, moved.Dimensions.X + 0.25f));
            PieceData recolored = resized.WithMaterial(
                piece.MaterialId == LabMaterialIds.Stone ? LabMaterialIds.Brick : LabMaterialIds.Stone);
            Measure("begin_edit", () => engine.BeginEdit(piece.Id));
            Measure("end_edit", () => engine.EndEdit(piece.Id));
            Measure("move", () => Require(history.Update(piece.Id, moved)));
            Measure("resize", () => Require(history.Update(piece.Id, resized)));
            Measure("material", () => Require(history.Update(piece.Id, recolored)));
            Measure("delete", () => Require(history.Delete(piece.Id)));
            Measure("undo_delete", () => RequireUndo(history));
            Measure("redo_delete", () => RequireRedo(history));
            Measure("undo_delete_again", () => RequireUndo(history));
            Measure("undo_material", () => RequireUndo(history));
            Measure("undo_resize", () => RequireUndo(history));
            Measure("undo_move", () => RequireUndo(history));
            if (world.Count != initialCount || engine.PieceCount != initialCount ||
                !world.TryGet(piece.Id, out PieceData restored) ||
                !restored.Transform.Equals(piece.Transform) ||
                !restored.Dimensions.Equals(piece.Dimensions) ||
                restored.MaterialId != piece.MaterialId || engine.DirtyRegionCount != 0)
                throw new InvalidOperationException("Edit probe did not restore the authored village.");
        }

        private static void Require(ConstructionChangeSet change)
        { if (!change.Changed) throw new InvalidOperationException("Edit produced no change."); }
        private static void RequireUndo(ConstructionCommandHistory history)
        { if (!history.TryUndo(out ConstructionChangeSet change) || !change.Changed)
            throw new InvalidOperationException("Undo failed."); }
        private static void RequireRedo(ConstructionCommandHistory history)
        { if (!history.TryRedo(out ConstructionChangeSet change) || !change.Changed)
            throw new InvalidOperationException("Redo failed."); }

        private IEnumerator Prepare(int target, List<PieceData> template, Action<string> result)
        {
            logicalMs = environmentMs = visualMs = double.NaN;
            environmentRenderers = 0;
            if (engine != null) { engine.Dispose(); engine = null; }
            if (visualRoot != null) Destroy(visualRoot);
            foreach (PvtGrandVillageEnvironment environment in environments) environment.Dispose();
            foreach (GameObject root in tileRoots) Destroy(root);
            environments.Clear(); tileRoots.Clear();
            yield return null;
            world = new ConstructionWorld();
            Stopwatch clock = Stopwatch.StartNew();
            for (int start = 0; start < target; start += 64)
            {
                int end = Math.Min(target, start + 64);
                try
                {
                    for (int i = start; i < end; i++)
                        if (!world.Create(PvtGrandVillageScaleScenario.PieceAt(template, i)).Changed)
                            throw new InvalidOperationException("Duplicate PieceId at " + i);
                }
                catch (Exception ex) { result(ex.ToString()); yield break; }
                if (UnsafeMemory() || clock.Elapsed.TotalSeconds > 1200)
                { result("Logical construction exceeded memory/time guard."); yield break; }
                yield return null;
            }
            logicalMs = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            int tiles = PvtGrandVillageScaleScenario.TileCount(target, template.Count);
            for (int tile = 0; tile < tiles; tile++)
            {
                var root = new GameObject("Grand village tile " + tile);
                root.transform.SetParent(transform, false);
                root.transform.localPosition = PvtGrandVillageScaleScenario.TileOffset(tile);
                tileRoots.Add(root);
                try
                {
                    environments.Add(new PvtGrandVillageEnvironment(root.transform, terrain, road, water,
                        dryAlbedo, dryNormal, dryMask, groundAlbedo, groundNormal, groundMask,
                        tree, pine, shrub, rock));
                    environmentRenderers += root.GetComponentsInChildren<Renderer>(true).Length;
                }
                catch (Exception ex) { result(ex.ToString()); yield break; }
                if (UnsafeMemory() || clock.Elapsed.TotalSeconds > 1200)
                { result("Environment construction exceeded memory/time guard."); yield break; }
                yield return null;
            }
            environmentMs = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            visualRoot = new GameObject("Grand scale combined architecture");
            visualRoot.transform.SetParent(transform, false);
            var registry = new MaterialRegistry(stone, stone, brick, plaster);
            registry.Register(PvtFinalScenario.Wood, wood);
            registry.Register(PvtFinalScenario.Ceramic, roof);
            registry.Register(PvtVisualCityScenario.Glass, glass);
            try { engine = new PvtChunkVisualEngine(world, registry, visualRoot.transform); }
            catch (Exception ex) { result(ex.ToString()); yield break; }
            while (engine.DirtyRegionCount > 0)
            {
                try { engine.RebuildDirty(1); }
                catch (Exception ex) { result(ex.ToString()); yield break; }
                if (UnsafeMemory() || clock.Elapsed.TotalSeconds > 1200)
                { result("Visual construction exceeded memory/time guard."); yield break; }
                yield return null;
            }
            visualMs = clock.Elapsed.TotalMilliseconds;
            if (engine.PieceCount != target || world.SpatialIndex.IndexedPieceCount != target)
                result("Logical, spatial and visual piece counts differ.");
        }

        private IEnumerator Capture(int target, int templateCount, int repeat, string view,
            Action<string> result)
        {
            Position(view, target, templateCount);
            Stopwatch wall = Stopwatch.StartNew();
            float elapsed = 0f;
            while (elapsed < WarmupSeconds)
            {
                if (UnsafeMemory() || wall.Elapsed.TotalSeconds > WallLimitSeconds)
                { result("Warmup exceeded memory/wall-time guard."); Write(target, repeat, view, templateCount, null, null, "failed", "warmup guard"); yield break; }
                yield return null; elapsed += Time.unscaledDeltaTime;
            }
            var frames = new List<float>(4096);
            var counters = new PvtFrameCounters();
            elapsed = 0f; wall.Restart();
            string failure = null;
            while (elapsed < CaptureSeconds)
            {
                if (UnsafeMemory() || wall.Elapsed.TotalSeconds > WallLimitSeconds)
                { failure = "Capture exceeded memory/wall-time guard."; result(failure); break; }
                yield return null;
                float dt = Time.unscaledDeltaTime;
                if (dt > 0f && !float.IsNaN(dt) && !float.IsInfinity(dt))
                { frames.Add(dt * 1000f); counters.Read(); elapsed += dt; }
            }
            counters.Dispose();
            if (frames.Count == 0 && failure == null)
            { failure = "No valid frame samples."; result(failure); }
            Write(target, repeat, view, templateCount, frames, counters,
                failure == null ? "ok" : "failed", failure ?? "");
        }

        private void Position(string view, int target, int templateCount)
        {
            int tiles = PvtGrandVillageScaleScenario.TileCount(target, templateCount);
            float width = Mathf.Min(tiles - 1, 15) * PvtGrandVillageScaleScenario.TileSpacing;
            float depth = ((tiles - 1) / 16) * PvtGrandVillageScaleScenario.TileSpacing;
            float extent = Mathf.Max(180f, width, depth);
            Vector3 point = view == "panorama" ? new Vector3(width * 0.5f, 8f, depth * 0.5f) :
                view == "street" ? new Vector3(-28f, 5f, -65f) : new Vector3(-22f, 16f, 38f);
            Vector3 eye = view == "panorama" ? point + new Vector3(0f,
                Mathf.Max(350f, extent * 0.7f), -Mathf.Max(600f, extent * 1.15f)) :
                view == "street" ? new Vector3(-35f, 8f, -78f) : new Vector3(-22f, 42f, -55f);
            sceneCamera.farClipPlane = view == "panorama" ? 8000f : 500f;
            RenderSettings.fogEndDistance = view == "panorama" ? 8000f : 210f;
            sceneCamera.transform.position = eye;
            sceneCamera.transform.LookAt(point);
        }

        private bool UnsafeMemory()
        {
            if (Profiler.GetTotalReservedMemoryLong() > (long)MemoryLimitMB * 1048576L) return true;
            try { using (Process process = Process.GetCurrentProcess())
                return process.WorkingSet64 > 10L * 1024L * 1048576L; }
            catch { return false; }
        }

        private static string ProcessMemory()
        {
            try { using (Process process = Process.GetCurrentProcess())
                return PvtBenchmarkCsv.Number(process.WorkingSet64 / 1048576d); }
            catch { return "unavailable"; }
        }

        private void Write(int target, int repeat, string view, int templateCount,
            List<float> frames, PvtFrameCounters counters, string status, string error)
        {
            if (frames != null) frames.Sort();
            double mean = double.NaN;
            if (frames != null && frames.Count > 0)
            { double sum = 0; foreach (float value in frames) sum += value; mean = sum / frames.Count; }
            string N(double value) => PvtBenchmarkCsv.Number(value);
            string M(List<float> values) => values == null ? "unavailable" : PvtBenchmarkCsv.Mean(values);
            string field = status == "ok" ? "ok" : "unavailable";
            string row = PvtBenchmarkCsv.Row(Header,
                DateTime.UtcNow.ToString("o"), Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable",
                Application.unityVersion, Application.isEditor ? "Editor" : Debug.isDebugBuild ? "DevelopmentPlayer" : "ReleasePlayer",
                SystemInfo.processorType, SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize.ToString(),
                SystemInfo.graphicsMemorySize > 0 ? SystemInfo.graphicsMemorySize.ToString() : "unavailable",
                "G" + target, world == null ? "unavailable" : world.Count.ToString(),
                tileRoots.Count.ToString(), repeat.ToString(), view,
                PvtGrandVillageScenario.Seed.ToString(), Screen.width.ToString(), Screen.height.ToString(),
                QualitySettings.vSyncCount.ToString(), Application.targetFrameRate.ToString(),
                N(logicalMs), N(environmentMs), N(visualMs),
                engine == null ? field : engine.RegionCount.ToString(),
                engine == null ? field : engine.PageCount.ToString(),
                environmentRenderers.ToString(),
                engine == null ? field : engine.VertexCount.ToString(),
                engine == null ? field : engine.TriangleCount.ToString(),
                frames == null ? field : frames.Count.ToString(), N(1000d / mean),
                N(1000d / PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.99d)),
                N(PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.5d)),
                N(PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.95d)),
                N(PvtBenchmarkCsv.Percentile(frames ?? new List<float>(), 0.99d)),
                frames == null || frames.Count == 0 ? field : N(frames[frames.Count - 1]),
                counters == null ? field : M(counters.Main), counters == null ? field : M(counters.Render),
                counters == null ? field : M(counters.Gpu), counters == null ? field : M(counters.Draws),
                counters == null ? field : M(counters.Batches), counters == null ? field : M(counters.SetPass),
                N(GC.GetTotalMemory(false) / 1048576d),
                Profiler.GetTotalReservedMemoryLong() > 0 ?
                    N(Profiler.GetTotalReservedMemoryLong() / 1048576d) : "unavailable",
                ProcessMemory(), "unavailable", status, error);
            File.AppendAllText(csvPath, row + Environment.NewLine);
            Debug.Log("Grand scale " + target + " repeat " + repeat + " " + view + ": " + status, this);
        }
    }
}
