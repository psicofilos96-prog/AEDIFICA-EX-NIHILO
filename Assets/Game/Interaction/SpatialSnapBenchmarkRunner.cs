using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Aedifica.Construction;
using Aedifica.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Aedifica.Interaction
{
    // E2b.1 benchmark of snap resolution only; no PieceView or per-piece GameObject.
    public sealed class SpatialSnapBenchmarkRunner : MonoBehaviour
    {
        private const int Samples = 128;
        private const int Repetitions = 3;
        private static readonly Func<long> AllocatedBytes = FindAllocationCounter();
        private string outputPath;

        private struct Result
        {
            public PieceData Piece;
            public bool HasTarget;
            public PieceId TargetId;
            public GeometricSnapKind Kind;
        }

        private IEnumerator Start()
        {
            outputPath = Path.Combine(Application.persistentDataPath,
                $"aedifica_E2b1_snap_{SpatialStressScenario.Seed}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(outputPath, Header + Environment.NewLine);
            bool failed = false;
            try { Warmup(); }
            catch (Exception error) { Failure("warmup", 0, 0, error); Debug.LogException(error, this); failed = true; }
            if (failed) yield break;
            yield return null;
            foreach (int count in SpatialStressScenario.Sizes)
            for (int repeat = 1; repeat <= Repetitions; repeat++)
            {
                if (SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 8192)
                {
                    Failure("L" + (Array.IndexOf(SpatialStressScenario.Sizes, count) + 1), count, repeat,
                        new InvalidOperationException("At least 8 GB RAM required; scenario size was not reduced."));
                    yield break;
                }
                try { Run(count, repeat); }
                catch (Exception error)
                {
                    Failure("L" + (Array.IndexOf(SpatialStressScenario.Sizes, count) + 1), count, repeat, error);
                    Debug.LogException(error, this);
                    failed = true;
                }
                if (failed) yield break;
                yield return null;
            }
            Debug.Log($"E2b.1 snap benchmark complete: {outputPath}", this);
        }

        private static ConstructionWorld World(int count)
        {
            var world = new ConstructionWorld(SpatialStressScenario.ChunkMeters);
            foreach (PieceData piece in SpatialStressScenario.Generate(count, SpatialStressScenario.Seed))
                if (!world.Create(piece).Changed) throw new InvalidOperationException("Duplicate benchmark ID.");
            return world;
        }

        private static PieceData Raw(int sample) => FreePieceCatalog.Create(PieceType.Block,
            PieceId.Parse("ffffffffffffffffffffffffffffffff"),
            new Vector3((sample % 16 - 8) * 2.4f, 0f, (sample / 16 - 4) * 2.4f), (sample % 4) * 15f);

        private static SnapSettings Settings() => new SnapSettings
        {
            SurfaceSnapEnabled = true, EdgeSnapEnabled = true, EndpointSnapEnabled = true
        };

        private void Warmup()
        {
            ConstructionWorld world = World(256);
            var settings = Settings();
            var full = new SnapResolver();
            var indexed = new SnapResolver();
            for (int i = 0; i < 12; i++)
            {
                PieceData raw = Raw(i);
                var session = new ManipulationSession(raw, ManipulationMode.Move, ManipulationAxis.X,
                    Vector2.zero, Vector2.right, 100f);
                full.Reset(); indexed.Reset();
                full.Resolve(raw, session, settings, world.Pieces);
                indexed.Resolve(raw, session, settings, SpatialSnapCandidates.ForMove(world, raw, settings));
            }
        }

        private void Run(int count, int repeat)
        {
            string scenario = "L" + (Array.IndexOf(SpatialStressScenario.Sizes, count) + 1);
            ConstructionWorld world = World(count);
            var settings = Settings();
            PieceData[] raw = Enumerable.Range(0, Samples).Select(Raw).ToArray();
            var sessions = raw.Select(piece => new ManipulationSession(piece, ManipulationMode.Move,
                ManipulationAxis.X, Vector2.zero, Vector2.right, 100f)).ToArray();
            var fullResults = new Result[Samples];
            var indexedResults = new Result[Samples];
            var candidateCounts = new int[Samples];
            var full = new SnapResolver();
            var indexed = new SnapResolver();

            void Full(int i)
            {
                full.Reset();
                PieceData result = full.Resolve(raw[i], sessions[i], settings, world.Pieces);
                fullResults[i] = new Result { Piece = result, HasTarget = full.HasTarget,
                    TargetId = full.HasTarget ? full.TargetId : default, Kind = full.HasTarget ? full.ActiveKind : default };
            }
            void Indexed(int i)
            {
                indexed.Reset();
                IReadOnlyList<PieceData> candidates = SpatialSnapCandidates.ForMove(world, raw[i], settings);
                candidateCounts[i] = candidates.Count;
                PieceData result = indexed.Resolve(raw[i], sessions[i], settings, candidates);
                indexedResults[i] = new Result { Piece = result, HasTarget = indexed.HasTarget,
                    TargetId = indexed.HasTarget ? indexed.TargetId : default, Kind = indexed.HasTarget ? indexed.ActiveKind : default };
            }

            // Alternate batch order between repetitions to expose cache/GC ordering effects.
            if (repeat % 2 == 0) { Measure("spatial", Indexed); Measure("linear", Full); }
            else { Measure("linear", Full); Measure("spatial", Indexed); }
            for (int i = 0; i < Samples; i++)
            {
                Result a = fullResults[i], b = indexedResults[i];
                if (!a.Piece.Transform.Equals(b.Piece.Transform) || !a.Piece.Dimensions.Equals(b.Piece.Dimensions) ||
                    a.HasTarget != b.HasTarget || a.HasTarget && (a.TargetId != b.TargetId || a.Kind != b.Kind))
                    throw new InvalidOperationException($"Snap result differs at sample {i}: linear={a.TargetId}, spatial={b.TargetId}.");
            }

            void Measure(string method, Action<int> action)
            {
                GC.Collect();
                if (GC.GetTotalMemory(false) > 1536L * 1024L * 1024L)
                    throw new OutOfMemoryException("Managed memory exceeded 1.5 GB; scenario size was not reduced.");
                long beforeMemory = GC.GetTotalMemory(false);
                long? beforeAlloc = ThreadAllocated();
                var times = new double[Samples];
                Stopwatch batch = Stopwatch.StartNew();
                for (int i = 0; i < Samples; i++)
                {
                    if (batch.Elapsed.TotalMinutes > 5d)
                        throw new TimeoutException("Snap batch exceeded five minutes; samples were not reduced.");
                    long start = Stopwatch.GetTimestamp();
                    action(i);
                    times[i] = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                }
                long? afterAlloc = ThreadAllocated();
                long afterMemory = GC.GetTotalMemory(false);
                Array.Sort(times);
                double total = times.Sum();
                string N(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
                Append(new[] {
                    DateTime.UtcNow.ToString("o"), scenario, SpatialStressScenario.Seed.ToString(),
                    Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable", Application.unityVersion,
                    Application.platform.ToString(), Application.isEditor ? "Editor" : "Player",
                    SystemInfo.processorType, SystemInfo.graphicsDeviceName,
                    SystemInfo.systemMemorySize > 0 ? SystemInfo.systemMemorySize.ToString() : "unavailable",
                    count.ToString(), world.SpatialIndex.OccupiedChunkCount.ToString(), repeat.ToString(), method,
                    Samples.ToString(), method == "spatial" ? N(candidateCounts.Average()) : N(count),
                    method == "spatial" ? candidateCounts.Min().ToString() : count.ToString(),
                    method == "spatial" ? candidateCounts.Max().ToString() : count.ToString(),
                    N(total), N(total / Samples), N(Percentile(times, 0.50d)), N(Percentile(times, 0.95d)),
                    N(Percentile(times, 0.99d)), beforeMemory.ToString(), afterMemory.ToString(),
                    beforeAlloc.HasValue && afterAlloc.HasValue ? (afterAlloc.Value - beforeAlloc.Value).ToString() : "unavailable",
                    "ok", ""
                });
            }
        }

        private static double Percentile(IReadOnlyList<double> sorted, double p) =>
            sorted[Math.Max(0, (int)Math.Ceiling(sorted.Count * p) - 1)];

        private static Func<long> FindAllocationCounter()
        {
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            if (method == null) return null;
            try { return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method); }
            catch { return null; }
        }

        private static long? ThreadAllocated()
        {
            if (AllocatedBytes == null) return null;
            try { return AllocatedBytes(); }
            catch { return null; }
        }

        private void Failure(string scenario, int count, int repeat, Exception error)
        {
            Append(new[] { DateTime.UtcNow.ToString("o"), scenario, SpatialStressScenario.Seed.ToString(),
                Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable", Application.unityVersion,
                Application.platform.ToString(), Application.isEditor ? "Editor" : "Player", SystemInfo.processorType,
                SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize.ToString(), count.ToString(), "unavailable",
                repeat.ToString(), "failure", "0", "unavailable", "unavailable", "unavailable", "unavailable",
                "unavailable", "unavailable", "unavailable", "unavailable", "unavailable", "unavailable",
                "unavailable", "failed", error.GetType().Name + ": " + error.Message });
        }

        private void Append(string[] fields)
        {
            for (int i = 0; i < fields.Length; i++) fields[i] = "\"" + (fields[i] ?? "").Replace("\"", "\"\"") + "\"";
            File.AppendAllText(outputPath, string.Join(",", fields) + Environment.NewLine);
        }

        private const string Header = "utc,scenario,seed,commit,unity,platform,environment,cpu,gpu,ram_mb_capacity,pieces,chunks,repeat,method,samples,candidates_mean,candidates_min,candidates_max,total_ms,mean_ms,p50_ms,p95_ms,p99_ms,managed_before_bytes,managed_after_bytes,thread_allocated_delta_bytes,status,error";
    }
}
