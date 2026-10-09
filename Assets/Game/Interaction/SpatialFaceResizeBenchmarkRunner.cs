using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Aedifica.Construction;
using Aedifica.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Aedifica.Interaction
{
    public sealed class SpatialFaceResizeRunContext
    {
        // A logical 50k-piece full scan can take longer than five minutes for 96 frames.
        public const int RunTimeoutMinutes = 20;
        public string Scenario;
        public int Pieces;
        public int Repeat;
        public string Method;
        public string Phase;
        public int Frame;
        public int FramesProcessed;
        public double ElapsedSeconds;

        public static bool TimedOut(double elapsedSeconds) => elapsedSeconds > RunTimeoutMinutes * 60d;

        public static bool IsDivergence(string phase, Exception error) =>
            phase != null && phase.EndsWith("/equivalence", StringComparison.Ordinal) &&
            error is InvalidOperationException;

        public string Describe() => $"scenario={Scenario}, pieces={Pieces}, repeat={Repeat}, " +
            $"method={Method}, phase={Phase}, frame={Frame}, framesProcessed={FramesProcessed}, " +
            $"elapsedSeconds={ElapsedSeconds.ToString("F3", CultureInfo.InvariantCulture)}";
    }

    // Logical snap benchmark: one resolver persists through every frame of each gesture.
    public sealed class SpatialFaceResizeBenchmarkRunner : MonoBehaviour
    {
        private const int Repetitions = 3;
        private const int Samples = ContinuousSnapWorkload.Cases * ContinuousSnapWorkload.Frames;
        private string outputPath;
        private SpatialFaceResizeRunContext activeRun;

        private struct Sample
        {
            public PieceData Piece;
            public bool Hit;
            public PieceId Id;
            public GeometricSnapKind Kind;
            public Vector3 Point;
        }

        private sealed class RunResult
        {
            public readonly Sample[] Results = new Sample[Samples];
            public readonly double[] Times = new double[Samples];
            public long Candidates;
            public int Hits;
            public int Transitions;
        }

        private IEnumerator Start()
        {
            outputPath = Path.Combine(Application.persistentDataPath,
                $"aedifica_E2b3_face_spatial_{ContinuousSnapWorkload.Seed}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(outputPath, Header + Environment.NewLine);
            try
            {
                activeRun = new SpatialFaceResizeRunContext { Scenario = "warmup", Pieces = 0,
                    Repeat = 0, Method = "setup", Phase = "warmup", Frame = -1 };
                ConstructionWorld warm = ContinuousSnapWorkload.World(0);
                RunMeasured(warm, 0, 0, false, false, "warmup/1");
                RunMeasured(warm, 0, 0, true, false, "warmup/2");
                RunMeasured(warm, 0, 0, false, true, "warmup/3");
                RunMeasured(warm, 0, 0, true, true, "warmup/4");
            }
            catch (Exception error)
            {
                Failure("warmup", 0, 0, error);
                Debug.LogError($"E2b.3 benchmark failed: {activeRun.Describe()}, " +
                    $"exception={error.GetType().Name}: {error.Message}", this);
                Debug.LogException(error, this);
                yield break;
            }
            yield return null;
            foreach (int count in SpatialStressScenario.Sizes)
            {
                string scenario = Scenario(count);
                activeRun = new SpatialFaceResizeRunContext { Scenario = scenario, Pieces = count,
                    Repeat = 0, Method = "setup", Phase = "scenario-start", Frame = -1 };
                Debug.Log($"E2b.3 scenario begin: {activeRun.Describe()}", this);
                if (SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 8192)
                {
                    var error = new InvalidOperationException("At least 8 GB RAM required; scenario size was not reduced.");
                    Failure(scenario, count, 0, error);
                    Debug.LogError($"E2b.3 benchmark failed: {activeRun.Describe()}, " +
                        $"exception={error.GetType().Name}: {error.Message}", this);
                    Debug.LogException(error, this);
                    yield break;
                }
                ConstructionWorld world;
                try { world = ContinuousSnapWorkload.World(count); }
                catch (Exception error)
                {
                    Failure(scenario, count, 0, error);
                    Debug.LogError($"E2b.3 benchmark failed: {activeRun.Describe()}, " +
                        $"exception={error.GetType().Name}: {error.Message}", this);
                    Debug.LogException(error, this);
                    yield break;
                }
                for (int repeat = 1; repeat <= Repetitions; repeat++)
                {
                    try
                    {
                        activeRun = new SpatialFaceResizeRunContext { Scenario = scenario, Pieces = count,
                            Repeat = repeat, Method = "resource_check", Phase = "before-repetition", Frame = -1 };
                        if (GC.GetTotalMemory(false) > 1536L * 1024L * 1024L)
                            throw new OutOfMemoryException("Managed memory exceeded 1.5 GB; scenario size was not reduced.");
                        MeasureRepetition(world, count, repeat);
                        Debug.Log($"E2b.3 repetition complete: scenario={scenario}, pieces={count}, " +
                            $"repeat={repeat}, methods=4, csv={outputPath}", this);
                    }
                    catch (Exception error)
                    {
                        Failure(scenario, count, repeat, error);
                        Debug.LogError($"E2b.3 benchmark failed: {activeRun.Describe()}, " +
                            $"exception={error.GetType().Name}: {error.Message}", this);
                        Debug.LogException(error, this);
                        yield break;
                    }
                    yield return null;
                }
                Debug.Log($"E2b.3 scenario complete: scenario={scenario}, pieces={count}, " +
                    $"repetitions={Repetitions}", this);
            }
            Debug.Log($"E2b.3 continuous snap benchmark complete: records=36, csv={outputPath}", this);
        }

        private void MeasureRepetition(ConstructionWorld world, int count, int repeat)
        {
            // Each path occupies both an outer and an inner position in every repetition.
            bool spatialFirst = repeat % 2 == 0;
            string sequence = spatialFirst ? "BAAB" : "ABBA";
            RunResult first = RunMeasured(world, count, repeat, spatialFirst, false, $"move/{sequence}/slot1");
            RunResult second = RunMeasured(world, count, repeat, !spatialFirst, false, $"move/{sequence}/slot2");
            RunResult third = RunMeasured(world, count, repeat, !spatialFirst, false, $"move/{sequence}/slot3");
            RunResult fourth = RunMeasured(world, count, repeat, spatialFirst, false, $"move/{sequence}/slot4");
            RunResult linearA = spatialFirst ? second : first;
            RunResult linearB = spatialFirst ? third : fourth;
            RunResult spatialA = spatialFirst ? first : second;
            RunResult spatialB = spatialFirst ? fourth : third;
            ComparisonContext(count, repeat, "move_linear_vs_move_spatial", "move/equivalence");
            Compare(linearA, spatialA, "move linear/spatial");
            Compare(linearB, spatialB, "move linear/spatial repeat");
            Compare(linearA, linearB, "move linear repeat");

            // Independent ABBA/BAAB order for Face Resize, with persistent gesture state.
            RunResult faceFirst = RunMeasured(world, count, repeat, spatialFirst, true, $"face/{sequence}/slot1");
            RunResult faceSecond = RunMeasured(world, count, repeat, !spatialFirst, true, $"face/{sequence}/slot2");
            RunResult faceThird = RunMeasured(world, count, repeat, !spatialFirst, true, $"face/{sequence}/slot3");
            RunResult faceFourth = RunMeasured(world, count, repeat, spatialFirst, true, $"face/{sequence}/slot4");
            RunResult faceLinearA = spatialFirst ? faceSecond : faceFirst;
            RunResult faceLinearB = spatialFirst ? faceThird : faceFourth;
            RunResult faceSpatialA = spatialFirst ? faceFirst : faceSecond;
            RunResult faceSpatialB = spatialFirst ? faceFourth : faceThird;
            ComparisonContext(count, repeat, "face_linear_vs_face_spatial", "face/equivalence");
            Compare(faceLinearA, faceSpatialA, "face linear/spatial");
            Compare(faceLinearB, faceSpatialB, "face linear/spatial repeat");
            Compare(faceLinearA, faceLinearB, "face linear repeat");
            activeRun = new SpatialFaceResizeRunContext { Scenario = Scenario(count), Pieces = count,
                Repeat = repeat, Method = "csv_write", Phase = "validated", Frame = Samples - 1,
                FramesProcessed = Samples };
            int chunks = world.SpatialIndex.OccupiedChunkCount;
            Write(Scenario(count), count, chunks, repeat, "move_linear", linearA, linearB);
            Write(Scenario(count), count, chunks, repeat, "move_spatial", spatialA, spatialB);
            Write(Scenario(count), count, chunks, repeat, "face_linear", faceLinearA, faceLinearB);
            Write(Scenario(count), count, chunks, repeat, "face_spatial", faceSpatialA, faceSpatialB);
        }

        private RunResult RunMeasured(ConstructionWorld world, int count, int repeat,
            bool spatial, bool face, string phase)
        {
            activeRun = new SpatialFaceResizeRunContext { Scenario = count == 0 ? "warmup" : Scenario(count),
                Pieces = count, Repeat = repeat, Method = (face ? "face" : "move") +
                (spatial ? "_spatial" : "_linear"), Phase = phase, Frame = -1 };
            Stopwatch duration = Stopwatch.StartNew();
            try { return Run(world, spatial, face, activeRun); }
            catch
            {
                activeRun.ElapsedSeconds = duration.Elapsed.TotalSeconds;
                throw;
            }
        }

        private void ComparisonContext(int count, int repeat, string method, string phase)
        {
            activeRun = new SpatialFaceResizeRunContext { Scenario = Scenario(count), Pieces = count,
                Repeat = repeat, Method = method, Phase = phase, Frame = -1,
                FramesProcessed = Samples };
        }

        private RunResult Run(ConstructionWorld world, bool spatial, bool face,
            SpatialFaceResizeRunContext progress)
        {
            var output = new RunResult();
            SnapSettings settings = ContinuousSnapWorkload.Settings();
            Stopwatch limit = Stopwatch.StartNew();
            Debug.Log($"E2b.3 run begin: {progress.Describe()}, totalFrames={Samples}, " +
                $"timeoutMinutes={SpatialFaceResizeRunContext.RunTimeoutMinutes}", this);
            for (int scenario = 0; scenario < ContinuousSnapWorkload.Cases; scenario++)
            {
                PieceData initial = ContinuousSnapWorkload.Initial(scenario);
                ManipulationSession session = ContinuousSnapWorkload.Session(initial, face);
                var resolver = new SnapResolver();
                bool previousHit = false;
                PieceId previousId = default;
                for (int frame = 0; frame < ContinuousSnapWorkload.Frames; frame++)
                {
                    int index = scenario * ContinuousSnapWorkload.Frames + frame;
                    progress.Frame = index;
                    progress.ElapsedSeconds = limit.Elapsed.TotalSeconds;
                    if (SpatialFaceResizeRunContext.TimedOut(progress.ElapsedSeconds))
                        throw new TimeoutException($"Individual Run exceeded " +
                            $"{SpatialFaceResizeRunContext.RunTimeoutMinutes} minutes; workload was not reduced. " +
                            progress.Describe());
                    PieceData raw = ContinuousSnapWorkload.Raw(initial, frame, face);
                    long start = Stopwatch.GetTimestamp();
                    IReadOnlyList<PieceData> candidates = spatial
                        ? (face ? SpatialSnapCandidates.ForFaceResize(world, raw, session, settings)
                            : SpatialSnapCandidates.ForMove(world, raw, settings)) : null;
                    PieceData result = resolver.Resolve(raw, session, settings, spatial ? candidates : world.Pieces);
                    output.Times[index] = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                    output.Candidates += spatial ? candidates.Count : world.Count;
                    bool hit = resolver.HasTarget;
                    if (hit) output.Hits++;
                    if (frame > 0 && (hit != previousHit || hit && previousHit && resolver.TargetId != previousId))
                        output.Transitions++;
                    output.Results[index] = new Sample { Piece = result, Hit = hit,
                        Id = hit ? resolver.TargetId : default, Kind = hit ? resolver.ActiveKind : default,
                        Point = hit ? resolver.TargetPoint : default };
                    previousHit = hit;
                    previousId = hit ? resolver.TargetId : default;
                    progress.FramesProcessed = index + 1;
                    progress.ElapsedSeconds = limit.Elapsed.TotalSeconds;
                    if (SpatialFaceResizeRunContext.TimedOut(progress.ElapsedSeconds))
                        throw new TimeoutException($"Individual Run exceeded " +
                            $"{SpatialFaceResizeRunContext.RunTimeoutMinutes} minutes; workload was not reduced. " +
                            progress.Describe());
                }
                // One log per 24-frame trajectory, outside the per-frame stopwatch.
                Debug.Log($"E2b.3 run progress: {progress.Describe()}, totalFrames={Samples}", this);
            }
            progress.ElapsedSeconds = limit.Elapsed.TotalSeconds;
            Debug.Log($"E2b.3 run complete: {progress.Describe()}, " +
                $"durationSeconds={progress.ElapsedSeconds.ToString("F3", CultureInfo.InvariantCulture)}", this);
            return output;
        }

        private static void Compare(RunResult expected, RunResult actual, string context)
        {
            for (int i = 0; i < Samples; i++)
            {
                Sample a = expected.Results[i], b = actual.Results[i];
                if (!a.Piece.Transform.Equals(b.Piece.Transform) || !a.Piece.Dimensions.Equals(b.Piece.Dimensions) ||
                    a.Hit != b.Hit || a.Hit && (a.Id != b.Id || a.Kind != b.Kind || !a.Point.Equals(b.Point)))
                    throw new InvalidOperationException($"{context}: frame={i}, scenario={i / ContinuousSnapWorkload.Frames}, " +
                        $"localFrame={i % ContinuousSnapWorkload.Frames}, expected={a.Id}/{a.Kind}/{a.Point}/" +
                        $"{a.Piece.Transform.Position}, actual={b.Id}/{b.Kind}/{b.Point}/{b.Piece.Transform.Position}.");
            }
            if (expected.Hits != actual.Hits || expected.Transitions != actual.Transitions)
                throw new InvalidOperationException($"{context}: hits or target transitions differ.");
        }

        private void Write(string scenario, int count, int chunks, int repeat, string method,
            RunResult first, RunResult second)
        {
            double[] times = first.Times.Concat(second.Times).ToArray();
            Array.Sort(times);
            string N(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
            Append(DateTime.UtcNow.ToString("o"), scenario, ContinuousSnapWorkload.Seed.ToString(),
                Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable", Application.unityVersion,
                Application.platform.ToString(), Application.isEditor ? "Editor" : "Player",
                SystemInfo.processorType, SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize.ToString(),
                count.ToString(), chunks.ToString(), repeat.ToString(), method, times.Length.ToString(),
                (first.Candidates + second.Candidates).ToString(), (first.Hits + second.Hits).ToString(),
                (first.Transitions + second.Transitions).ToString(), "0", N(times.Sum()), N(Percentile(times, .5)),
                N(Percentile(times, .95)), N(Percentile(times, .99)), "ok", "");
        }

        private static double Percentile(double[] sorted, double p) =>
            sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * p) - 1)];

        private static string Scenario(int count) => "L" + (Array.IndexOf(SpatialStressScenario.Sizes, count) + 1);

        private void Failure(string scenario, int count, int repeat, Exception error) => Append(
            DateTime.UtcNow.ToString("o"), scenario, ContinuousSnapWorkload.Seed.ToString(),
            Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable", Application.unityVersion,
            Application.platform.ToString(), Application.isEditor ? "Editor" : "Player", SystemInfo.processorType,
            SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize.ToString(), count.ToString(), "unavailable",
            repeat.ToString(), activeRun?.Method ?? "setup", "0", "unavailable", "unavailable", "unavailable",
            SpatialFaceResizeRunContext.IsDivergence(activeRun?.Phase, error) ? "1" : "unavailable",
            "unavailable", "unavailable",
            "unavailable", "unavailable", "failed", (activeRun?.Describe() ?? "context=unavailable") +
                ", exception=" + error.GetType().Name + ": " + error.Message);

        private void Append(params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++) fields[i] = "\"" + (fields[i] ?? "").Replace("\"", "\"\"") + "\"";
            File.AppendAllText(outputPath, string.Join(",", fields) + Environment.NewLine);
        }

        private const string Header = "utc,scenario,seed,commit,unity,platform,environment,cpu,gpu,ram_mb_capacity,pieces,chunks,repeat,method,samples,candidate_visits,matched_samples,target_transitions,divergences,total_ms,p50_ms,p95_ms,p99_ms,status,error";
    }
}
