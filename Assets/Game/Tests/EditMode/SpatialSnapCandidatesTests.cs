using System;
using System.Collections.Generic;
using System.Linq;
using Aedifica.Construction;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class SpatialSnapCandidatesTests
    {
        private static PieceId Id(int value) => PieceId.Parse(value.ToString("x32"));
        private static PieceData Block(int id, Vector3 position, float width = 1f, float yaw = 0f) =>
            new PieceData(Id(id), new PieceTransform(position, Quaternion.Euler(0f, yaw, 0f)),
                new BlockDimensions(width, 1f, 1f));
        private static SnapSettings Settings(GeometricSnapKind kind) => new SnapSettings
        {
            SurfaceSnapEnabled = kind == GeometricSnapKind.Surface,
            EdgeSnapEnabled = kind == GeometricSnapKind.Edge,
            EndpointSnapEnabled = kind == GeometricSnapKind.Endpoint
        };
        private static ManipulationSession Move(PieceData piece) => new ManipulationSession(piece,
            ManipulationMode.Move, ManipulationAxis.X, Vector2.zero, Vector2.right, 100f);

        private static void Equivalent(SnapResolver full, SnapResolver indexed, ConstructionWorld world,
            PieceData raw, ManipulationSession session, SnapSettings settings)
        {
            PieceData expected = full.Resolve(raw, session, settings, world.Pieces);
            PieceData actual = indexed.Resolve(raw, session, settings, SpatialSnapCandidates.ForMove(world, raw, settings));
            Assert.That(actual.Transform.Equals(expected.Transform), Is.True);
            Assert.That(actual.Dimensions.Equals(expected.Dimensions), Is.True);
            Assert.That(indexed.HasTarget, Is.EqualTo(full.HasTarget));
            if (full.HasTarget)
            {
                Assert.That(indexed.TargetId, Is.EqualTo(full.TargetId));
                Assert.That(indexed.ActiveKind, Is.EqualTo(full.ActiveKind));
                Assert.That(indexed.TargetPoint, Is.EqualTo(full.TargetPoint));
            }
        }

        [TestCase(GeometricSnapKind.Surface)]
        [TestCase(GeometricSnapKind.Edge)]
        [TestCase(GeometricSnapKind.Endpoint)]
        public void RotatedMultiChunkNegativeAndBoundaryTargetsMatchFullScan(GeometricSnapKind kind)
        {
            var world = new ConstructionWorld();
            PieceData raw = Block(9, new Vector3(-64f, 0f, 64f), 1f, 45f);
            world.Create(raw);
            PieceData near = Block(1, raw.Transform.Position + Quaternion.Euler(0f, 45f, 0f) * Vector3.right * 1.1f,
                1f, 45f);
            PieceData farLarge = Block(2, new Vector3(-160f, 0f, 64f), 90f, 45f);
            world.Create(farLarge);
            world.Create(near);
            var settings = Settings(kind);
            Equivalent(new SnapResolver(), new SnapResolver(), world, raw, Move(raw), settings);
            CollectionAssert.Contains(SpatialSnapCandidates.ForMove(world, raw, settings).Select(p => p.Id).ToArray(), near.Id);
            CollectionAssert.DoesNotContain(SpatialSnapCandidates.ForMove(world, raw, settings).Select(p => p.Id).ToArray(), farLarge.Id);
        }

        [Test]
        public void TieLockAndRemovalMatchFullScanAcrossSuccessiveGestures()
        {
            var world = new ConstructionWorld();
            PieceData moving = Block(9, Vector3.zero);
            PieceData left = Block(1, new Vector3(-1.1f, 0f, 0f));
            PieceData right = Block(2, new Vector3(1.1f, 0f, 0f));
            world.Create(right); world.Create(moving); world.Create(left);
            var settings = Settings(GeometricSnapKind.Endpoint);
            var full = new SnapResolver();
            var indexed = new SnapResolver();
            ManipulationSession session = Move(moving);
            Equivalent(full, indexed, world, moving, session, settings);
            Assert.That(indexed.TargetId, Is.EqualTo(left.Id));
            PieceData shifted = moving.WithTransform(new PieceTransform(new Vector3(-0.2f, 0f, 0f), Quaternion.identity));
            Equivalent(full, indexed, world, shifted, session, settings); // held within release distance
            world.Delete(left.Id);
            Equivalent(full, indexed, world, moving, session, settings); // lock released or replaced
            world.Update(right.Id, right.WithTransform(new PieceTransform(new Vector3(128f, 0f, 0f), Quaternion.identity)));
            Equivalent(full, indexed, world, moving, session, settings);
            Assert.That(indexed.HasTarget, Is.False);
            world.Update(right.Id, right.WithTransform(new PieceTransform(new Vector3(1.1f, 0f, 0f), Quaternion.Euler(0f, 45f, 0f))));
            Equivalent(full, indexed, world, moving, session, settings);
            world.Update(right.Id, right.WithDimensions(new PieceDimensions(new BlockDimensions(2f, 1f, 1f))));
            Equivalent(full, indexed, world, moving, session, settings);
        }

        [Test]
        public void DistantFaceResizeStillUsesCompleteWorldScan()
        {
            var world = new ConstructionWorld();
            PieceData moving = Block(9, Vector3.zero);
            PieceData distant = Block(1, new Vector3(20f, 0f, 0f));
            world.Create(distant);
            var settings = Settings(GeometricSnapKind.Endpoint);
            var face = new ManipulationSession(moving, ManipulationMode.Resize, ManipulationAxis.X,
                Vector2.zero, Vector2.right, 100f, settings, ResizeMode.Face, 1);
            PieceData snapped = new SnapResolver().Resolve(moving, face, settings, world.Pieces);
            Assert.That(snapped.Dimensions.Equals(moving.Dimensions), Is.True,
                "The current resolver still applies its total-distance capture threshold.");
            Assert.That(SpatialSnapCandidates.ForMove(world, moving, settings), Is.Empty,
                "The distant target is intentionally excluded only from Move.");
        }

        [Test]
        public void CatalogSnapFeaturesRemainInsideIndexedWorldEnvelopes()
        {
            int id = 1;
            foreach (PieceType type in FreePieceCatalog.Types)
            foreach (float yaw in new[] { 0f, 45f, 90f })
            {
                PieceData piece = FreePieceCatalog.Create(type, Id(id++), new Vector3(-64f, 0f, 64f), yaw);
                Bounds bounds = ConstructionChangeSet.WorldBounds(piece);
                bounds.Expand(0.02f);
                var features = new List<SnapFeature>();
                SnapGeometry.Collect(piece, features);
                foreach (SnapFeature feature in features)
                {
                    Assert.That(bounds.Contains(feature.A), Is.True, $"{type} yaw={yaw} feature={feature.Kind}/{feature.Index} A");
                    if (feature.Kind == GeometricSnapKind.Edge)
                        Assert.That(bounds.Contains(feature.B), Is.True, $"{type} yaw={yaw} edge={feature.Index} B");
                    if (feature.Kind != GeometricSnapKind.Surface) continue;
                    Vector3 origin = feature.Triangle ? feature.A - (feature.U + feature.V) / 3f : feature.A;
                    Assert.That(bounds.Contains(origin), Is.True, $"{type} yaw={yaw} surface={feature.Index} origin");
                    if (feature.Triangle)
                    {
                        Assert.That(bounds.Contains(origin + feature.U), Is.True);
                        Assert.That(bounds.Contains(origin + feature.V), Is.True);
                    }
                    else
                        foreach (int u in new[] { -1, 1 }) foreach (int v in new[] { -1, 1 })
                            Assert.That(bounds.Contains(origin + u * feature.U + v * feature.V), Is.True,
                                $"{type} yaw={yaw} surface={feature.Index} corner=({u},{v})");
                }
            }
        }
    }
}
