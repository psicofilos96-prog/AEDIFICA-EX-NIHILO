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
    // Independent integrated-world experiment. It never replaces ConstructionLab or PVT-1.
    public sealed class Pvt2IntegratedRunner : MonoBehaviour
    {
        private static readonly string[] Cameras = { "fixed", "street", "overview", "path" };
        public const string FramesHeader = "utc,session,commit,worktree,unity,environment,scenario,repeat,camera,seed,pieces,visual_regions,visual_pages,terrain_m,foliage_regions,foliage_renderers,screen_width,screen_height,quality,vsync,target_fps,logical_create_ms,visual_build_ms,frames,fps_mean,fps_p01,fps_p05,fps_p50,frame_mean_ms,frame_p50_ms,frame_p95_ms,frame_p99_ms,frame_max_ms,stutter_33ms,stutter_50ms,cpu_main_ms,cpu_render_ms,gpu_ms,draw_calls,batches,setpass,managed_mb,unity_allocated_mb,unity_reserved_mb,process_rss_mb,vram_used_mb,stream_events,stream_ms_total,edit_events,edit_ms_total,save_ms,load_ms,save_bytes,integrity,status,error";
        public const string EventsHeader = "utc,session,scenario,repeat,camera,operation,milliseconds,rebuild_regions,world_pieces,visual_pieces,status,error";

        [SerializeField] private Camera benchmarkCamera;
        [SerializeField] private Material neutralMaterial, stoneMaterial, brickMaterial, plasterMaterial;
        [SerializeField] private int seed = Pvt2Scenario.Seed;
        [SerializeField] private int batchSize = 64;
        [SerializeField] private int maxReservedMB = 4096;
        [SerializeField] private float warmupSeconds = 10f;
        [SerializeField] private float captureSeconds = 20f;
        [SerializeField] private float maxCaptureWallSeconds = 90f;

        private ConstructionWorld world;
        private MaterialRegistry materials;
        private PvtChunkVisualEngine engine;
        private Pvt2Environment landscape;
        private GameObject visualRoot;
        private string session, framesPath, eventsPath, manifestPath, failure;
        private readonly HashSet<string> frameKeys = new HashSet<string>();
        private readonly List<string> pendingEvents = new List<string>();
        private bool running, previewReady;
        private bool failureRecorded;
        private int editCycle;
        private double logicalCreateMs, visualBuildMs;

        public void Configure(Camera camera, Material neutral, Material stone, Material brick, Material plaster)
        {
            benchmarkCamera=camera; neutralMaterial=neutral; stoneMaterial=stone;
            brickMaterial=brick; plasterMaterial=plaster;
        }

        private IEnumerator Start()
        {
            if (benchmarkCamera == null || neutralMaterial == null || batchSize < 1 ||
                captureSeconds < 18f || warmupSeconds < 0f || maxCaptureWallSeconds < captureSeconds)
            {
                Debug.LogError("PVT-2 invalid scene configuration.",this);
                yield break;
            }
            world = new ConstructionWorld();
            materials = new MaterialRegistry(neutralMaterial,stoneMaterial,brickMaterial,plasterMaterial);
            try { landscape = new Pvt2Environment(transform,seed); }
            catch (Exception error)
            {
                failure=error.GetType().Name+": "+error.Message;
                Debug.LogError("PVT-2 environment setup failed: "+failure,this);
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-pvt2-run") >= 0)
            {
                running=true;
                yield return RunBenchmark();
            }
            else
            {
                if (failure != null) yield break;
                yield return BuildScenario(10000,1);
                previewReady = failure == null;
                Debug.Log(previewReady ? "PVT-2 preview A ready. Inspect the scene, then press Run Benchmark."
                    : "PVT-2 preview failed: " + failure,this);
            }
        }

        private void OnGUI()
        {
            if (!previewReady || running) return;
            if (GUI.Button(new Rect(20,20,210,45),"Run PVT-2 Benchmark"))
            {
                running=true;
                StartCoroutine(RunBenchmark());
            }
        }

        private IEnumerator RunBenchmark()
        {
            previewReady=false;
            if (!Application.isEditor) Screen.SetResolution(1920,1080,false);
            session=$"aedifica_PVT2_{seed}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}";
            Directory.CreateDirectory(Application.persistentDataPath);
            framesPath=Path.Combine(Application.persistentDataPath,session+"_frames.csv");
            eventsPath=Path.Combine(Application.persistentDataPath,session+"_events.csv");
            manifestPath=Path.Combine(Application.persistentDataPath,session+"_manifest.txt");
            File.WriteAllText(framesPath,FramesHeader+Environment.NewLine);
            File.WriteAllText(eventsPath,EventsHeader+Environment.NewLine);
            File.WriteAllLines(manifestPath,new[] {
                "session="+session,"utc_start="+DateTime.UtcNow.ToString("o"),
                "commit="+Commit,"worktree="+Worktree,"unity="+Application.unityVersion,
                "environment="+(Application.isEditor ? "Editor" : Debug.isDebugBuild ? "DevelopmentPlayer" : "ReleasePlayer"),
                "cpu="+SystemInfo.processorType,"gpu="+SystemInfo.graphicsDeviceName,
                "ram_capacity_mb="+SystemInfo.systemMemorySize,"vram_capacity_mb="+SystemInfo.graphicsMemorySize,
                "resolution="+Screen.width+"x"+Screen.height,"vsync="+QualitySettings.vSyncCount,
                "target_fps="+Application.targetFrameRate,"quality="+QualitySettings.GetQualityLevel(),
                "seed="+seed,"terrain_m=1000x1000","foliage_stream_cell_m="+Pvt2Environment.CellSize,
                "vram_used_mb=unavailable: no reliable Unity process VRAM API",
                "render_thread_ms=unavailable when recorder has no valid samples"
            });
            Debug.Log($"PVT-2 started: {session}, frames={framesPath}, events={eventsPath}",this);
            for (int scenario=0; scenario<Pvt2Scenario.Sizes.Length && failure==null; scenario++)
            {
                int target=Pvt2Scenario.Sizes[scenario];
                yield return BuildScenario(target,scenario+1);
                if (failure != null) break;
                Dictionary<PieceId,PieceData> original=PvtIntegrity.Capture(world);
                for (int repeat=1; repeat<=3 && failure==null; repeat++)
                foreach (string mode in Cameras)
                {
                    yield return Capture(scenario,target,repeat,mode,original);
                    if (failure != null) break;
                }
                if (failure == null && !PvtIntegrity.Matches(world,original))
                    failure="Scenario logical/spatial integrity changed after captures.";
            }
            CompleteMissingRows();
            File.AppendAllLines(manifestPath,new[] {
                "utc_end="+DateTime.UtcNow.ToString("o"),
                "frame_rows="+frameKeys.Count,
                "complete="+(failure==null && frameKeys.Count==36),
                "error="+(failure ?? "none")
            });
            Debug.Log($"PVT-2 finished: status={(failure==null ? "ok" : "failed")}, frames={framesPath}, events={eventsPath}, error={failure ?? "none"}",this);
            running=false;
        }

        private IEnumerator BuildScenario(int target,int density)
        {
            if (engine != null) { engine.Dispose(); engine=null; }
            if (visualRoot != null) { Destroy(visualRoot); visualRoot=null; yield return null; }
            world=new ConstructionWorld();
            Stopwatch timer=Stopwatch.StartNew();
            while (world.Count<target)
            {
                if (timer.Elapsed.TotalSeconds>1200d ||
                    GC.GetTotalMemory(false)>maxReservedMB*1048576L ||
                    Profiler.GetTotalReservedMemoryLong()>maxReservedMB*1048576L)
                { failure="Logical generation time/memory limit reached."; yield break; }
                int end=Math.Min(target,world.Count+batchSize);
                try
                {
                    while (world.Count<end)
                        if (!world.Create(Pvt2Scenario.PieceAt(world.Count,seed)).Changed)
                            throw new InvalidOperationException("Duplicate PVT-2 piece ID.");
                }
                catch (Exception error) { failure=error.GetType().Name+": "+error.Message; }
                if (failure != null) yield break;
                yield return null;
            }
            logicalCreateMs=timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            visualRoot=new GameObject("PVT-2 combined architecture");
            visualRoot.transform.SetParent(transform,false);
            try { engine=new PvtChunkVisualEngine(world,materials,visualRoot.transform); }
            catch (Exception error) { failure=error.GetType().Name+": "+error.Message; }
            if (failure != null) yield break;
            while (engine.DirtyRegionCount>0)
            {
                if (timer.Elapsed.TotalSeconds>1200d || Profiler.GetTotalReservedMemoryLong()>maxReservedMB*1048576L)
                { failure="Visual build time/memory limit reached."; yield break; }
                try { engine.RebuildDirty(1); }
                catch (Exception error) { failure=error.GetType().Name+": "+error.Message; }
                if (failure != null) yield break;
                yield return null;
            }
            visualBuildMs=timer.Elapsed.TotalMilliseconds;
            PositionCamera("fixed",0f);
            try { landscape.UpdateStreaming(benchmarkCamera.transform.position,density); }
            catch (Exception error) { failure=error.GetType().Name+": "+error.Message; }
            if (failure != null) yield break;
            Debug.Log($"PVT-2 scenario {Scenario(target)} prepared: pieces={world.Count}, pages={engine.PageCount}, foliage={landscape.VegetationRenderers}, logicalMs={logicalCreateMs:F1}, visualMs={visualBuildMs:F1}",this);
        }

        private IEnumerator Capture(int scenario,int target,int repeat,string mode,
            IReadOnlyDictionary<PieceId,PieceData> original)
        {
            PositionCamera(mode,0f);
            if (repeat==1 && mode=="fixed")
            {
                yield return null;
                string image=Path.Combine(Application.persistentDataPath,session+"_"+Scenario(target)+"_fixed.png");
                try { ScreenCapture.CaptureScreenshot(image); Debug.Log("PVT-2 reference image requested: "+image,this); }
                catch (Exception error) { Debug.LogWarning("PVT-2 reference image unavailable: "+error.Message,this); }
            }
            int density=scenario+1;
            float warmup=0f;
            Stopwatch warmupWall=Stopwatch.StartNew();
            while (warmup<warmupSeconds)
            {
                if (warmupWall.Elapsed.TotalSeconds>maxCaptureWallSeconds)
                { failure="Warmup exceeded wall-time safety limit."; break; }
                if (Profiler.GetTotalReservedMemoryLong()>maxReservedMB*1048576L)
                { failure="Warmup exceeded Unity reserved-memory safety limit."; break; }
                if (mode=="path") PositionCamera(mode,warmupSeconds>0f ? warmup/warmupSeconds : 0f);
                try { UpdateLandscape(mode,density); }
                catch (Exception error) { failure=error.GetType().Name+": "+error.Message; break; }
                yield return null;
                warmup+=Time.unscaledDeltaTime;
            }
            if (failure != null)
            {
                WriteFrame(target,repeat,mode,new List<float>(),null,0,double.NaN,0,double.NaN,
                    double.NaN,double.NaN,0,false,"failed",failure);
                yield break;
            }
            PositionCamera(mode,0f);
            UpdateLandscape(mode,density);
            yield return null;
            var frames=new List<float>(4096);
            var counters=new PvtFrameCounters();
            int streamEvents=0, editEvents=0;
            double streamMs=0d, editMs=0d;
            double saveMs=double.NaN, loadMs=double.NaN;
            long saveBytes=0;
            float elapsed=0f, nextEdit=1f;
            Pvt2EditWorkload.Session editSession=null;
            try { editSession=new Pvt2EditWorkload.Session(world,engine,seed,++editCycle); }
            catch (Exception error)
            {
                counters.Dispose();
                failure=error.GetType().Name+": "+error.Message;
                WriteFrame(target,repeat,mode,frames,null,0,double.NaN,0,double.NaN,
                    double.NaN,double.NaN,0,false,"failed",failure);
            }
            if (failure != null) yield break;
            Stopwatch wall=Stopwatch.StartNew();
            while (elapsed<captureSeconds)
            {
                if (wall.Elapsed.TotalSeconds>maxCaptureWallSeconds)
                { failure="Capture exceeded wall-time safety limit."; break; }
                if (Profiler.GetTotalReservedMemoryLong()>maxReservedMB*1048576L)
                { failure="Capture exceeded Unity reserved-memory safety limit."; break; }
                if (mode=="path") PositionCamera(mode,elapsed/captureSeconds);
                try
                {
                    if (UpdateLandscape(mode,density))
                    { streamEvents++; streamMs+=landscape.LastTransitionMs; }
                    if (!editSession.Complete && elapsed>=nextEdit)
                    {
                        Pvt2EditWorkload.Sample sample=editSession.Next();
                        editEvents++; editMs+=sample.Milliseconds;
                        WriteEvent(target,repeat,mode,sample.Operation,sample.Milliseconds,sample.RebuiltRegions,"ok","");
                        nextEdit+=1f;
                    }
                    if (repeat==1 && mode=="fixed" && editSession.Complete && elapsed>=16f && double.IsNaN(saveMs))
                    {
                        string savePath=Path.Combine(Application.persistentDataPath,session+"_"+Scenario(target)+".pvt2");
                        Stopwatch measure=Stopwatch.StartNew();
                        saveBytes=Pvt2Snapshot.Save(world,savePath);
                        measure.Stop(); saveMs=measure.Elapsed.TotalMilliseconds;
                        WriteEvent(target,repeat,mode,"save",(float)saveMs,0,"ok","");
                        measure.Restart();
                        ConstructionWorld loaded=Pvt2Snapshot.Load(savePath);
                        measure.Stop(); loadMs=measure.Elapsed.TotalMilliseconds;
                        bool valid=Pvt2Snapshot.Matches(loaded,original);
                        WriteEvent(target,repeat,mode,"load",(float)loadMs,0,valid ? "ok" : "failed",
                            valid ? "" : "Save/load integrity mismatch.");
                        if (!valid) throw new InvalidOperationException("Save/load integrity mismatch.");
                    }
                }
                catch (Exception error) { failure=error.GetType().Name+": "+error.Message; break; }
                yield return null;
                float dt=Time.unscaledDeltaTime;
                elapsed+=dt;
                if (dt>0f && !float.IsNaN(dt) && !float.IsInfinity(dt))
                { frames.Add(dt*1000f); counters.Read(); }
            }
            counters.Dispose();
            if (pendingEvents.Count>0)
            {
                File.AppendAllLines(eventsPath,pendingEvents);
                pendingEvents.Clear();
            }
            if (!editSession.Complete && failure==null) failure="Edit session did not finish during capture.";
            if (repeat==1 && mode=="fixed" && double.IsNaN(saveMs) && failure==null)
                failure="Save/load did not run during capture.";
            if (frames.Count==0 && failure==null) failure="No valid frame samples.";
            bool integrity=failure==null && PvtIntegrity.Matches(world,original) &&
                engine.PieceCount==world.Count && engine.DirtyRegionCount==0;
            if (!integrity && failure==null) failure="Capture integrity mismatch.";
            WriteFrame(target,repeat,mode,frames,counters,streamEvents,streamMs,editEvents,editMs,
                saveMs,loadMs,saveBytes,integrity,failure==null ? "ok" : "failed",failure ?? "");
            Debug.Log($"PVT-2 {Scenario(target)} repeat={repeat} {mode}: frames={frames.Count}, edits={editEvents}, streaming={streamEvents}, status={(failure==null ? "ok" : "failed")}",this);
        }

        private void PositionCamera(string mode,float progress)
        {
            Vector3 position;
            if (mode=="street") position=new Vector3(0f,20f,-38f);
            else if (mode=="overview") position=new Vector3(0f,500f,-650f);
            else if (mode=="path") position=new Vector3(Mathf.Sin(progress*Mathf.PI*2f)*300f,105f,
                -Mathf.Cos(progress*Mathf.PI*2f)*300f);
            else position=new Vector3(0f,160f,-240f);
            benchmarkCamera.transform.position=position;
            benchmarkCamera.transform.LookAt(new Vector3(0f,14f,0f));
        }

        private bool UpdateLandscape(string mode,int density)
        {
            Vector3 center = mode=="fixed" || mode=="overview"
                ? Vector3.zero : benchmarkCamera.transform.position;
            int radius = mode=="overview" ? 8 : mode=="fixed" ? 6 : mode=="path" ? 4 : 2;
            return landscape.UpdateStreaming(center,density,radius);
        }

        private void WriteFrame(int target,int repeat,string mode,List<float> frames,PvtFrameCounters counters,
            int streamEvents,double streamMs,int editEvents,double editMs,double saveMs,double loadMs,
            long saveBytes,bool integrity,string status,string error)
        {
            var row=NewRow(FramesHeader);
            Put(FramesHeader,row,"utc",DateTime.UtcNow.ToString("o"));
            Put(FramesHeader,row,"session",session);
            Put(FramesHeader,row,"commit",Commit);
            Put(FramesHeader,row,"worktree",Worktree);
            Put(FramesHeader,row,"unity",Application.unityVersion);
            Put(FramesHeader,row,"environment",Application.isEditor ? "Editor" : Debug.isDebugBuild ? "DevelopmentPlayer" : "ReleasePlayer");
            Put(FramesHeader,row,"scenario",Scenario(target));
            Put(FramesHeader,row,"repeat",repeat.ToString()); Put(FramesHeader,row,"camera",mode);
            Put(FramesHeader,row,"seed",seed.ToString());
            Put(FramesHeader,row,"pieces",world != null ? world.Count.ToString() : "unavailable");
            Put(FramesHeader,row,"visual_regions",engine != null ? engine.RegionCount.ToString() : "unavailable");
            Put(FramesHeader,row,"visual_pages",engine != null ? engine.PageCount.ToString() : "unavailable");
            Put(FramesHeader,row,"terrain_m","1000x1000");
            Put(FramesHeader,row,"foliage_regions",landscape != null ? landscape.LoadedRegions.ToString() : "unavailable");
            Put(FramesHeader,row,"foliage_renderers",landscape != null ? landscape.VegetationRenderers.ToString() : "unavailable");
            Put(FramesHeader,row,"screen_width",Screen.width.ToString());
            Put(FramesHeader,row,"screen_height",Screen.height.ToString());
            Put(FramesHeader,row,"quality",QualitySettings.GetQualityLevel().ToString());
            Put(FramesHeader,row,"vsync",QualitySettings.vSyncCount.ToString());
            Put(FramesHeader,row,"target_fps",Application.targetFrameRate.ToString());
            if (engine != null)
            {
                Put(FramesHeader,row,"logical_create_ms",PvtBenchmarkCsv.Number(logicalCreateMs));
                Put(FramesHeader,row,"visual_build_ms",PvtBenchmarkCsv.Number(visualBuildMs));
            }
            Put(FramesHeader,row,"frames",frames.Count.ToString());
            if (frames.Count>0)
            {
                double sum=0d, maximum=0d; int above33=0,above50=0;
                foreach (float frame in frames)
                { sum+=frame; maximum=Math.Max(maximum,frame); if (frame>33.3f) above33++; if (frame>50f) above50++; }
                frames.Sort();
                double mean=sum/frames.Count;
                Put(FramesHeader,row,"fps_mean",PvtBenchmarkCsv.Number(1000d/mean));
                Put(FramesHeader,row,"fps_p01",PvtBenchmarkCsv.Number(1000d/PvtBenchmarkCsv.Percentile(frames,0.99d)));
                Put(FramesHeader,row,"fps_p05",PvtBenchmarkCsv.Number(1000d/PvtBenchmarkCsv.Percentile(frames,0.95d)));
                Put(FramesHeader,row,"fps_p50",PvtBenchmarkCsv.Number(1000d/PvtBenchmarkCsv.Percentile(frames,0.5d)));
                Put(FramesHeader,row,"frame_mean_ms",PvtBenchmarkCsv.Number(mean));
                Put(FramesHeader,row,"frame_p50_ms",PvtBenchmarkCsv.Number(PvtBenchmarkCsv.Percentile(frames,0.5d)));
                Put(FramesHeader,row,"frame_p95_ms",PvtBenchmarkCsv.Number(PvtBenchmarkCsv.Percentile(frames,0.95d)));
                Put(FramesHeader,row,"frame_p99_ms",PvtBenchmarkCsv.Number(PvtBenchmarkCsv.Percentile(frames,0.99d)));
                Put(FramesHeader,row,"frame_max_ms",PvtBenchmarkCsv.Number(maximum));
                Put(FramesHeader,row,"stutter_33ms",above33.ToString());
                Put(FramesHeader,row,"stutter_50ms",above50.ToString());
            }
            if (counters!=null)
            {
                Put(FramesHeader,row,"cpu_main_ms",PvtBenchmarkCsv.Mean(counters.Main));
                Put(FramesHeader,row,"cpu_render_ms",PvtBenchmarkCsv.Mean(counters.Render));
                Put(FramesHeader,row,"gpu_ms",PvtBenchmarkCsv.Mean(counters.Gpu));
                Put(FramesHeader,row,"draw_calls",PvtBenchmarkCsv.Mean(counters.Draws));
                Put(FramesHeader,row,"batches",PvtBenchmarkCsv.Mean(counters.Batches));
                Put(FramesHeader,row,"setpass",PvtBenchmarkCsv.Mean(counters.SetPass));
            }
            Put(FramesHeader,row,"managed_mb",PositiveMemory(GC.GetTotalMemory(false)));
            Put(FramesHeader,row,"unity_allocated_mb",PositiveMemory(Profiler.GetTotalAllocatedMemoryLong()));
            Put(FramesHeader,row,"unity_reserved_mb",PositiveMemory(Profiler.GetTotalReservedMemoryLong()));
            long? rss=ProcessRss();
            if (rss.HasValue && rss.Value>0) Put(FramesHeader,row,"process_rss_mb",PvtBenchmarkCsv.Number(rss.Value/1048576d));
            Put(FramesHeader,row,"stream_events",streamEvents.ToString());
            Put(FramesHeader,row,"stream_ms_total",PvtBenchmarkCsv.Number(streamMs));
            Put(FramesHeader,row,"edit_events",editEvents.ToString());
            Put(FramesHeader,row,"edit_ms_total",PvtBenchmarkCsv.Number(editMs));
            Put(FramesHeader,row,"save_ms",PvtBenchmarkCsv.Number(saveMs));
            Put(FramesHeader,row,"load_ms",PvtBenchmarkCsv.Number(loadMs));
            if (saveBytes>0) Put(FramesHeader,row,"save_bytes",saveBytes.ToString());
            Put(FramesHeader,row,"integrity",integrity ? "pass" : "fail");
            Put(FramesHeader,row,"status",status); Put(FramesHeader,row,"error",error);
            File.AppendAllText(framesPath,PvtBenchmarkCsv.Row(FramesHeader,row)+Environment.NewLine);
            frameKeys.Add(Scenario(target)+":"+repeat+":"+mode);
            if (status=="failed") failureRecorded=true;
        }

        private void WriteEvent(int target,int repeat,string mode,string operation,float milliseconds,
            int regions,string status,string error)
        {
            string[] row={ DateTime.UtcNow.ToString("o"),session,Scenario(target),repeat.ToString(),mode,
                operation,PvtBenchmarkCsv.Number(milliseconds),regions.ToString(),world.Count.ToString(),
                engine.PieceCount.ToString(),status,error };
            pendingEvents.Add(PvtBenchmarkCsv.Row(EventsHeader,row));
        }

        private void CompleteMissingRows()
        {
            foreach (int target in Pvt2Scenario.Sizes)
            for (int repeat=1; repeat<=3; repeat++)
            foreach (string mode in Cameras)
            {
                if (frameKeys.Contains(Scenario(target)+":"+repeat+":"+mode)) continue;
                string status = failure != null && !failureRecorded ? "failed" : "skipped";
                WriteFrame(target,repeat,mode,new List<float>(),null,0,double.NaN,0,double.NaN,
                    double.NaN,double.NaN,0,false,status,failure ?? "Run incomplete.");
            }
        }

        private static string[] NewRow(string header)
        {
            var row=new string[header.Split(',').Length];
            for (int i=0;i<row.Length;i++) row[i]="unavailable";
            return row;
        }
        private static void Put(string header,string[] row,string name,string value)
        {
            int index=Array.IndexOf(header.Split(','),name);
            if (index<0) throw new InvalidOperationException("Missing CSV column: "+name);
            row[index]=value ?? "unavailable";
        }
        private static long? ProcessRss()
        {
            try { using (Process process=Process.GetCurrentProcess()) return process.WorkingSet64; }
            catch { return null; }
        }
        private static string PositiveMemory(long bytes) => bytes>0
            ? PvtBenchmarkCsv.Number(bytes/1048576d) : "unavailable";
        private static string Scenario(int target) => target==10000 ? "A" : target==25000 ? "B" : "C";
        private static string Commit => Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable";
        private static string Worktree => Environment.GetEnvironmentVariable("AEDIFICA_WORKTREE") ?? "unavailable";

        private void OnDestroy()
        {
            if (engine!=null) engine.Dispose();
            if (landscape!=null) landscape.Dispose();
        }
    }
}
