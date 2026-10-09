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
    // Logical snap benchmark: one resolver persists through every frame of each gesture.
    public sealed class ContinuousSnapBenchmarkRunner : MonoBehaviour
    {
        private const int Repetitions = 3;
        private const int Samples = ContinuousSnapWorkload.Cases * ContinuousSnapWorkload.Frames;
        private string outputPath;

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
                $"aedifica_E2b2_continuous_{ContinuousSnapWorkload.Seed}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(outputPath, Header + Environment.NewLine);
            try
            {
                ConstructionWorld warm = ContinuousSnapWorkload.World(0);
                Run(warm, false, false);
                Run(warm, true, false);
            }
            catch (Exception error)
            {
                Failure("warmup", 0, 0, error);
                Debug.LogException(error, this);
                yield break;
            }
            yield return null;
            foreach (int count in SpatialStressScenario.Sizes)
            {
                if (SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 8192)
                {
                    Failure(Scenario(count), count, 0,
                        new InvalidOperationException("At least 8 GB RAM required; scenario size was not reduced."));
                    yield break;
                }
                ConstructionWorld world;
                try { world = ContinuousSnapWorkload.World(count); }
                catch (Exception error)
                {
                    Failure(Scenario(count), count, 0, error);
                    Debug.LogException(error, this);
                    yield break;
                }
                for (int repeat = 1; repeat <= Repetitions; repeat++)
                {
                    try
                    {
                        if (GC.GetTotalMemory(false) > 1536L * 1024L * 1024L)
                            throw new OutOfMemoryException("Managed memory exceeded 1.5 GB; scenario size was not reduced.");
                        MeasureRepetition(world, count, repeat);
                    }
                    catch (Exception error)
                    {
                        Failure(Scenario(count), count, repeat, error);
                        Debug.LogException(error, this);
                        yield break;
                    }
                    yield return null;
                }
            }
            Debug.Log($"E2b.2 continuous snap benchmark complete: {outputPath}", this);
        }

        private void MeasureRepetition(ConstructionWorld world, int count, int repeat)
        {
            // Each path occupies both an outer and an inner position in every repetition.
            bool spatialFirst = repeat % 2 == 0;
            RunResult first = Run(world, spatialFirst, false);
            RunResult second = Run(world, !spatialFirst, false);
            RunResult third = Run(world, !spatialFirst, false);
            RunResult fourth = Run(world, spatialFirst, false);
            RunResult linearA = spatialFirst ? second : first;
            RunResult linearB = spatialFirst ? third : fourth;
            RunResult spatialA = spatialFirst ? first : second;
            RunResult spatialB = spatialFirst ? fourth : third;
            Compare(linearA, spatialA, "move linear/spatial");
            Compare(linearB, spatialB, "move linear/spatial repeat");
            Compare(linearA, linearB, "move linear repeat");

            // Face Resize remains full scan; compare repeated runs, without a spatial claim.
            RunResult faceA = Run(world, false, true);
            RunResult faceB = Run(world, false, true);
            Compare(faceA, faceB, "face full-scan repeat");
            int chunks = world.SpatialIndex.OccupiedChunkCount;
            Write(Scenario(count), count, chunks, repeat, "move_linear", linearA, linearB);
            Write(Scenario(count), count, chunks, repeat, "move_spatial", spatialA, spatialB);
            Write(Scenario(count), count, chunks, repeat, "face_linear", faceA, faceB);
        }

        private static RunResult Run(ConstructionWorld world, bool spatial, bool face)
        {
            var output = new RunResult();
            SnapSettings settings = ContinuousSnapWorkload.Settings();
            Stopwatch limit = Stopwatch.StartNew();
            for (int scenario = 0; scenario < ContinuousSnapWorkload.Cases; scenario++)
            {
                PieceData initial = ContinuousSnapWorkload.Initial(scenario);
                ManipulationSession session = ContinuousSnapWorkload.Session(initial, face);
                var resolver = new SnapResolver();
                bool previousHit = false;
                PieceId previousId = default;
                for (int frame = 0; frame < ContinuousSnapWorkload.Frames; frame++)
                {
                    if (limit.Elapsed.TotalMinutes > 5d)
                        throw new TimeoutException("Continuous snap run exceeded five minutes; workload was not reduced.");
                    int index = scenario * ContinuousSnapWorkload.Frames + frame;
                    PieceData raw = ContinuousSnapWorkload.Raw(initial, frame, face);
                    long start = Stopwatch.GetTimestamp();
                    IReadOnlyList<PieceData> candidates = spatial
                        ? SpatialSnapCandidates.ForMove(world, raw, settings) : null;
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
                }
            }
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
            repeat.ToString(), "failure", "0", "unavailable", "unavailable", "unavailable",
            error.Message.Contains("frame=") ? "1" : "unavailable", "unavailable", "unavailable",
            "unavailable", "unavailable", "failed", error.GetType().Name + ": " + error.Message);

        private void Append(params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++) fields[i] = "\"" + (fields[i] ?? "").Replace("\"", "\"\"") + "\"";
            File.AppendAllText(outputPath, string.Join(",", fields) + Environment.NewLine);
        }

        private const string Header = "utc,scenario,seed,commit,unity,platform,environment,cpu,gpu,ram_mb_capacity,pieces,chunks,repeat,method,samples,candidate_visits,matched_samples,target_transitions,divergences,total_ms,p50_ms,p95_ms,p99_ms,status,error";
    }
}
