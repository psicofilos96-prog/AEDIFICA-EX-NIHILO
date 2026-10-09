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
        public void CapturedTargetSurvivesCloserCompetitorInsideReleaseBandThenReleases()
        {
            var world = new ConstructionWorld();
            PieceData moving = Block(9, Vector3.zero);
            PieceData first = Block(1, new Vector3(-1.1f, 0f, 0f));
            PieceData competitor = Block(2, new Vector3(1.1f, 0f, 0f));
            world.Create(competitor);
            world.Create(first);
            var settings = Settings(GeometricSnapKind.Endpoint);
            var full = new SnapResolver();
            var indexed = new SnapResolver();
            ManipulationSession session = Move(moving);
            Equivalent(full, indexed, world, moving, session, settings);
            Assert.That(full.TargetId, Is.EqualTo(first.Id));

            PieceData inBand = moving.WithTransform(new PieceTransform(new Vector3(0.2f, 0f, 0f), Quaternion.identity));
            float firstGap = inBand.Transform.Position.x - moving.Dimensions.X * 0.5f -
                (first.Transform.Position.x + first.Dimensions.X * 0.5f);
            float competitorGap = Mathf.Abs(competitor.Transform.Position.x - competitor.Dimensions.X * 0.5f -
                (inBand.Transform.Position.x + moving.Dimensions.X * 0.5f));
            Assert.That(firstGap, Is.GreaterThan(settings.CaptureDistance));
            Assert.That(firstGap, Is.LessThanOrEqualTo(settings.ReleaseDistance));
            Assert.That(competitorGap, Is.LessThan(firstGap));
            Equivalent(full, indexed, world, inBand, session, settings);
            Assert.That(full.TargetId, Is.EqualTo(first.Id), "The locked feature must beat a closer new target.");

            PieceData beyond = moving.WithTransform(new PieceTransform(new Vector3(0.35f, 0f, 0f), Quaternion.identity));
            float beyondGap = beyond.Transform.Position.x - moving.Dimensions.X * 0.5f -
                (first.Transform.Position.x + first.Dimensions.X * 0.5f);
            Assert.That(beyondGap, Is.GreaterThan(settings.ReleaseDistance));
            Equivalent(full, indexed, world, beyond, session, settings);
            Assert.That(full.HasTarget && full.TargetId == first.Id, Is.False,
                "The first target must be released or replaced beyond ReleaseDistance.");
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
            foreach (Vector3 position in new[] { new Vector3(-64f, 0f, 64f), new Vector3(1000000f, 0f, -1000000f) })
            {
                AssertFeaturesInsideSearchMargin(FreePieceCatalog.Create(type, Id(id++), position, yaw));
            }

            PieceData longBlock = Block(id++, new Vector3(-64f, 0f, 64f), 150f, 45f);
            AssertFeaturesInsideSearchMargin(longBlock);
            PieceData wall = new PieceData(Id(id++),
                new PieceTransform(new Vector3(-128f, 0f, -64f), Quaternion.Euler(0f, 45f, 0f)),
                new WallDimensions(120f, 4f, 0.3f));
            wall = wall.WithOpening(new WallOpening(Guid.Parse("00000000-0000-0000-0000-000000000001"),
                wall.Id, WallOpeningKind.Passage,
                50f, 0f, 2f, 2.5f));
            AssertFeaturesInsideSearchMargin(wall);
        }

        [Test]
        public void LongTargetWithDistantCenterStillAppearsWhenItsFaceIsNear()
        {
            var world = new ConstructionWorld();
            PieceData target = Block(1, new Vector3(-64f, 0f, -64f), 150f);
            PieceData moving = Block(9, new Vector3(11.1f, 0f, -64f));
            world.Create(target);
            Assert.That(Vector3.Distance(target.Transform.Position, moving.Transform.Position), Is.GreaterThan(70f));
            CollectionAssert.Contains(SpatialSnapCandidates.ForMove(world, moving, Settings(GeometricSnapKind.Surface))
                .Select(piece => piece.Id).ToArray(), target.Id);
            foreach (GeometricSnapKind kind in new[] { GeometricSnapKind.Surface, GeometricSnapKind.Edge, GeometricSnapKind.Endpoint })
                Equivalent(new SnapResolver(), new SnapResolver(), world, moving, Move(moving), Settings(kind));
        }

        private static void AssertFeaturesInsideSearchMargin(PieceData piece)
        {
            Bounds bounds = ConstructionChangeSet.WorldBounds(piece);
            Vector3 center = bounds.center, extent = bounds.extents;
            float magnitude = Mathf.Max(Mathf.Max(Mathf.Abs(center.x) + extent.x,
                Mathf.Abs(center.y) + extent.y), Mathf.Abs(center.z) + extent.z);
            // Half the production margin: a stricter envelope test than the actual query.
            bounds.Expand(2f * (0.0005f + magnitude * 0.0000005f));
            var features = new List<SnapFeature>();
            SnapGeometry.Collect(piece, features);
            foreach (SnapFeature feature in features)
            {
                string context = $"{piece.Type} position={piece.Transform.Position} yaw={piece.Transform.Rotation.eulerAngles.y} feature={feature.Kind}/{feature.Index}";
                Assert.That(bounds.Contains(feature.A), Is.True, context + " A");
                if (feature.Kind == GeometricSnapKind.Edge)
                    Assert.That(bounds.Contains(feature.B), Is.True, context + " B");
                if (feature.Kind != GeometricSnapKind.Surface) continue;
                Vector3 origin = feature.Triangle ? feature.A - (feature.U + feature.V) / 3f : feature.A;
                Assert.That(bounds.Contains(origin), Is.True, context + " origin");
                if (feature.Triangle)
                {
                    Assert.That(bounds.Contains(origin + feature.U), Is.True, context + " triangle U");
                    Assert.That(bounds.Contains(origin + feature.V), Is.True, context + " triangle V");
                }
                else
                    foreach (int u in new[] { -1, 1 }) foreach (int v in new[] { -1, 1 })
                        Assert.That(bounds.Contains(origin + u * feature.U + v * feature.V), Is.True,
                            context + $" corner=({u},{v})");
            }
        }
    }
}
