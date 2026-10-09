using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Aedifica.Construction;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Aedifica.Rendering
{
    // Independent logical E2a benchmark. Never creates a PieceView or GameObject per piece.
    public sealed class SpatialStressLabRunner : MonoBehaviour
    {
        private const int Repetitions = 3;
        private const int QuerySamples = 128;
        private const long MaximumManagedBytes = 1536L * 1024L * 1024L;
        private static readonly Func<long> ThreadAllocationCounter = FindThreadAllocationCounter();
        private string outputPath;
        private int sink;

        private IEnumerator Start()
        {
            outputPath = Path.Combine(Application.persistentDataPath,
                $"aedifica_E2a_{SpatialStressScenario.Seed}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(outputPath, Header + Environment.NewLine);
            Debug.Log($"E2a logical benchmark started; CSV: {outputPath}", this);
            bool failed = false;
            try { Warmup(); }
            catch (Exception error) { WriteFailure("warmup", 0, 0, error); Debug.LogException(error, this); failed = true; }
            if (failed) yield break;
            yield return null;
            foreach (int size in SpatialStressScenario.Sizes)
            {
                for (int repetition = 1; repetition <= Repetitions; repetition++)
                {
                    if (SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 8192)
                    {
                        WriteFailure($"L{Array.IndexOf(SpatialStressScenario.Sizes, size) + 1}", size, repetition,
                            new InvalidOperationException("E2a requires at least 8 GB RAM; scenario size was not reduced."));
                        yield break;
                    }
                    GC.Collect();
                    try { RunScenario(size, repetition); }
                    catch (Exception error)
                    {
                        WriteFailure($"L{Array.IndexOf(SpatialStressScenario.Sizes, size) + 1}", size, repetition, error);
                        Debug.LogException(error, this);
                        failed = true;
                    }
                    if (failed) yield break;
                    yield return null; // Release the prior world before the next repetition.
                }
            }
            Debug.Log($"E2a complete: {outputPath}; sink={sink}", this);
        }

        private void Warmup()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var world = new ConstructionWorld(SpatialStressScenario.ChunkMeters);
                PieceData[] pieces = SpatialStressScenario.Generate(256, SpatialStressScenario.Seed).ToArray();
                foreach (PieceData piece in pieces) world.Create(piece);
                var history = new ConstructionCommandHistory(world);
                history.Update(pieces[0].Id, pieces[0].WithMaterial(LabMaterialIds.Stone));
                history.TryUndo(out _);
                history.TryRedo(out _);
                for (int probe = 0; probe < 3; probe++)
                {
                    sink += world.SpatialIndex.Query(SpatialStressScenario.Region(probe)).Count;
                    sink += world.SpatialIndex.QueryNearby(SpatialStressScenario.NearbyCenter(probe), 12f).Count;
                }
            }
        }

        private void RunScenario(int count, int repetition)
        {
            string scenario = $"L{Array.IndexOf(SpatialStressScenario.Sizes, count) + 1}";
            PieceData[] pieces = SpatialStressScenario.Generate(count, SpatialStressScenario.Seed).ToArray();
            var reference = pieces.ToDictionary(piece => piece.Id);
            var world = new ConstructionWorld(SpatialStressScenario.ChunkMeters);
            var history = new ConstructionCommandHistory(world, 2048);
            Measure(scenario, repetition, world, "create", count, i =>
            {
                if (!world.Create(pieces[i]).Changed) throw new InvalidOperationException("Create was rejected.");
            });
            SpatialStressScenario.Validate(world, reference);
            for (int probe = 0; probe < 3; probe++)
            {
                int selectedProbe = probe;
                string density = probe == 0 ? "dense" : probe == 1 ? "empty" : "boundary";
                Bounds area = SpatialStressScenario.Region(probe);
                Vector3 center = SpatialStressScenario.NearbyCenter(probe);
                Measure(scenario, repetition, world, "region_" + density, QuerySamples,
                    _ => sink += world.SpatialIndex.Query(area).Count);
                Measure(scenario, repetition, world, "nearby_" + density, QuerySamples,
                    _ => sink += world.SpatialIndex.QueryNearby(center, 12f).Count);
                Measure(scenario, repetition, world, "linear_region_" + density, 12,
                    _ => sink += SpatialStressScenario.LinearRegion(reference.Values, SpatialStressScenario.Region(selectedProbe)).Count);
                Measure(scenario, repetition, world, "linear_nearby_" + density, 12,
                    _ => sink += SpatialStressScenario.LinearNearby(reference.Values, center, 12f).Count);
            }

            int changedCount = Math.Min(256, count / 4);
            PieceData[] candidates = new PieceData[changedCount];
            for (int i = 0; i < changedCount; i++)
                candidates[i] = pieces[i].WithTransform(new PieceTransform(pieces[i].Transform.Position + Vector3.right * 65f,
                    pieces[i].Transform.Rotation));
            Apply("move", candidates);
            for (int i = 0; i < changedCount; i++)
            {
                PieceData current = reference[pieces[i].Id];
                candidates[i] = current.WithTransform(new PieceTransform(current.Transform.Position,
                    Quaternion.Euler(0f, current.Transform.Rotation.eulerAngles.y + 30f, 0f)));
            }
            Apply("rotate", candidates);
            for (int i = 0; i < changedCount; i++)
            {
                PieceData current = reference[pieces[i].Id];
                candidates[i] = current.WithDimensions(current.Dimensions.Resize(0, current.Dimensions.X + 1f));
            }
            Apply("resize", candidates);
            for (int i = 0; i < changedCount; i++) candidates[i] = reference[pieces[i].Id].WithMaterial(LabMaterialIds.Stone);
            Apply("material", candidates);

            int deletedCount = Math.Min(128, changedCount);
            Measure(scenario, repetition, world, "delete", deletedCount, i =>
            {
                if (!history.Delete(pieces[i].Id).Changed) throw new InvalidOperationException("Delete was rejected.");
            });
            for (int i = 0; i < deletedCount; i++)
            {
                reference.Remove(pieces[i].Id);
                if (world.SpatialIndex.ChunksFor(pieces[i].Id).Count != 0)
                    throw new InvalidOperationException("Deleted piece retained chunk membership.");
                if (world.SpatialIndex.Query(ConstructionChangeSet.WorldBounds(candidates[i])).Contains(pieces[i].Id))
                    throw new InvalidOperationException("Deleted piece remained in a spatial query.");
            }
            SpatialStressScenario.Validate(world, reference);
            Measure(scenario, repetition, world, "undo", deletedCount, i =>
            {
                if (!history.TryUndo(out _)) throw new InvalidOperationException("Undo failed.");
            });
            for (int i = 0; i < deletedCount; i++) reference.Add(pieces[i].Id, candidates[i]);
            SpatialStressScenario.Validate(world, reference);
            Measure(scenario, repetition, world, "redo", deletedCount, i =>
            {
                if (!history.TryRedo(out _)) throw new InvalidOperationException("Redo failed.");
            });
            for (int i = 0; i < deletedCount; i++) reference.Remove(pieces[i].Id);
            SpatialStressScenario.Validate(world, reference);

            // Mixed read/write batch after redo. Writes target pieces outside the deleted range.
            Measure(scenario, repetition, world, "mixed", 256, i =>
            {
                if (i % 2 == 0) sink += world.SpatialIndex.Query(SpatialStressScenario.Region(i)).Count;
                else
                {
                    PieceId id = pieces[deletedCount + (i % (count - deletedCount))].Id;
                    PieceData current = reference[id];
                    PieceData next = current.WithTransform(new PieceTransform(current.Transform.Position + Vector3.forward * 0.25f,
                        current.Transform.Rotation));
                    if (!history.Update(id, next).Changed) throw new InvalidOperationException("Mixed update failed.");
                    reference[id] = next;
                }
            });
            SpatialStressScenario.Validate(world, reference);
            AuditInvalidation(world, reference, pieces[deletedCount].Id);

            void Apply(string operation, PieceData[] replacements)
            {
                Measure(scenario, repetition, world, operation, replacements.Length, i =>
                {
                    if (!history.Update(replacements[i].Id, replacements[i]).Changed)
                        throw new InvalidOperationException(operation + " update was rejected.");
                });
                foreach (PieceData replacement in replacements) reference[replacement.Id] = replacement;
                SpatialStressScenario.Validate(world, reference);
            }
        }

        private static void AuditInvalidation(ConstructionWorld world, Dictionary<PieceId, PieceData> reference, PieceId id)
        {
            PieceData original = reference[id];
            var before = new HashSet<ChunkCoordinate>(world.SpatialIndex.ChunksFor(id));
            ConstructionInvalidation? notification = null;
            void Observe(ConstructionInvalidation change) { notification = change; }
            world.ChunksInvalidated += Observe;
            try
            {
                PieceData updated = original.WithTransform(new PieceTransform(
                    original.Transform.Position + Vector3.right * (SpatialStressScenario.ChunkMeters + 1f),
                    original.Transform.Rotation));
                if (!world.Update(id, updated).Changed) throw new InvalidOperationException("Invalidation audit update failed.");
                before.UnionWith(world.SpatialIndex.ChunksFor(id));
                if (!notification.HasValue || !before.SetEquals(notification.Value.Chunks))
                    throw new InvalidOperationException("Invalidation included missing or unrelated chunks.");
                if (!world.Update(id, original).Changed)
                    throw new InvalidOperationException("Invalidation audit restore failed.");
            }
            finally { world.ChunksInvalidated -= Observe; }
            SpatialStressScenario.Validate(world, reference);
        }

        private void Measure(string scenario, int repetition, ConstructionWorld world,
            string operation, int samples, Action<int> action)
        {
            GC.Collect(); // Validation and prior batches are outside the timed window.
            var times = new double[samples];
            long? allocationsBefore = ThreadAllocated();
            long managedBefore = GC.GetTotalMemory(false);
            if (managedBefore > MaximumManagedBytes)
                throw new OutOfMemoryException("Managed memory exceeded the E2a 1.5 GB guard; scenario size was not reduced.");
            long unityBefore = Profiler.GetTotalAllocatedMemoryLong();
            Stopwatch batchClock = Stopwatch.StartNew();
            for (int i = 0; i < samples; i++)
            {
                if (i % 1024 == 0 && batchClock.Elapsed.TotalMinutes > 5d)
                    throw new TimeoutException("E2a operation batch exceeded five minutes; remaining samples were not hidden or reduced.");
                long start = Stopwatch.GetTimestamp();
                action(i);
                times[i] = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            }
            long? allocationsAfter = ThreadAllocated();
            long managedAfter = GC.GetTotalMemory(false);
            long unityAfter = Profiler.GetTotalAllocatedMemoryLong();
            Array.Sort(times);
            double total = 0d;
            foreach (double duration in times) total += duration;
            string Number(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
            string[] fields = {
                DateTime.UtcNow.ToString("o"), scenario, SpatialStressScenario.Seed.ToString(),
                Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable", Application.unityVersion,
                Application.platform.ToString(), Application.isEditor ? "Editor" : "Player",
                SystemInfo.processorType, SystemInfo.graphicsDeviceName,
                SystemInfo.systemMemorySize > 0 ? SystemInfo.systemMemorySize.ToString() : "unavailable",
                world.Count.ToString(), world.SpatialIndex.OccupiedChunkCount.ToString(), repetition.ToString(), operation,
                samples.ToString(), Number(total), Number(total / samples), Number(Percentile(times, 0.50d)),
                samples >= 100 ? Number(Percentile(times, 0.95d)) : "unavailable",
                samples >= 100 ? Number(Percentile(times, 0.99d)) : "unavailable",
                managedBefore.ToString(), managedAfter.ToString(), (managedAfter - managedBefore).ToString(),
                unityBefore.ToString(), unityAfter.ToString(),
                allocationsBefore.HasValue && allocationsAfter.HasValue ? (allocationsAfter.Value - allocationsBefore.Value).ToString() : "unavailable",
                "ok", ""
            };
            Append(fields);
        }

        public static double Percentile(IReadOnlyList<double> sortedSamples, double probability)
        {
            if (sortedSamples == null || sortedSamples.Count == 0) throw new ArgumentException("At least one sample is required.");
            if (probability < 0d || probability > 1d) throw new ArgumentOutOfRangeException(nameof(probability));
            int index = Math.Max(0, (int)Math.Ceiling(sortedSamples.Count * probability) - 1);
            return sortedSamples[index];
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
            if (ThreadAllocationCounter == null) return null;
            try { return ThreadAllocationCounter(); }
            catch { return null; }
        }

        private void WriteFailure(string scenario, int count, int repetition, Exception error)
        {
            string[] fields = {
                DateTime.UtcNow.ToString("o"), scenario, SpatialStressScenario.Seed.ToString(),
                Environment.GetEnvironmentVariable("AEDIFICA_COMMIT") ?? "unavailable", Application.unityVersion,
                Application.platform.ToString(), Application.isEditor ? "Editor" : "Player",
                SystemInfo.processorType, SystemInfo.graphicsDeviceName,
                SystemInfo.systemMemorySize > 0 ? SystemInfo.systemMemorySize.ToString() : "unavailable",
                count.ToString(), "unavailable", repetition.ToString(), "failure", "0", "unavailable", "unavailable",
                "unavailable", "unavailable", "unavailable", "unavailable", "unavailable", "unavailable",
                "unavailable", "unavailable", "unavailable", "failed", error.GetType().Name + ": " + error.Message
            };
            Append(fields);
        }

        private void Append(string[] fields)
        {
            for (int i = 0; i < fields.Length; i++) fields[i] = "\"" + (fields[i] ?? "").Replace("\"", "\"\"") + "\"";
            File.AppendAllText(outputPath, string.Join(",", fields) + Environment.NewLine);
        }

        private const string Header = "utc,scenario,seed,commit,unity,platform,environment,cpu,gpu,ram_mb_capacity,pieces,chunks,repeat,operation,samples,total_ms,mean_ms,p50_ms,p95_ms,p99_ms,managed_before_bytes,managed_after_bytes,managed_delta_bytes,unity_allocated_before_bytes,unity_allocated_after_bytes,thread_allocated_delta_bytes,status,error";
    }
}
