using System.Collections;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aedifica.Tests.PlayMode
{
    public sealed class PvtChunkVisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator NegativeChunksAndLongRotatedPiecesRetainFullVisibleBounds()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var root = new GameObject("PVT bounds test");
            var world = new ConstructionWorld();
            PieceData longWall = new PieceData(PieceId.Parse("77777777777777777777777777777777"),
                new PieceTransform(new Vector3(-0.5f, 0f, -0.5f), Quaternion.Euler(0f, 45f, 0f)),
                new WallDimensions(100f, 3f, 0.4f));
            Assert.That(world.Create(longWall).Changed, Is.True);
            var engine = new PvtChunkVisualEngine(world, new MaterialRegistry(material), root.transform, 16f);
            try
            {
                Assert.That(engine.OwnerOf(longWall.Id), Is.EqualTo(new ChunkCoordinate(-1, 0, -1)));
                Assert.That(engine.RebuildDirty(), Is.EqualTo(1));
                MeshRenderer renderer = root.GetComponentInChildren<MeshRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Bounds logical = ConstructionChangeSet.WorldBounds(longWall);
                Assert.That(renderer.bounds.min.x, Is.LessThanOrEqualTo(logical.min.x + 0.001f));
                Assert.That(renderer.bounds.max.x, Is.GreaterThanOrEqualTo(logical.max.x - 0.001f));
                Assert.That(renderer.bounds.min.z, Is.LessThanOrEqualTo(logical.min.z + 0.001f));
                Assert.That(renderer.bounds.max.z, Is.GreaterThanOrEqualTo(logical.max.z - 0.001f));
                Assert.That(PvtIntegrity.Matches(world, PvtIntegrity.Capture(world)), Is.True);
            }
            finally
            {
                engine.Dispose(); Object.Destroy(root); Object.Destroy(material);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CombinedPagesResolvePieceIdAndRebuildOnlyChangedRegions()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var root = new GameObject("PVT visual test");
            var world = new ConstructionWorld();
            PieceData a = PvtScenario.PieceAt(0);
            PieceData b = PvtScenario.PieceAt(2);
            PieceData c = PvtScenario.PieceAt(5);
            Assert.That(world.Create(a).Changed && world.Create(b).Changed && world.Create(c).Changed, Is.True);
            var history = new ConstructionCommandHistory(world);
            var engine = new PvtChunkVisualEngine(world, new MaterialRegistry(material), root.transform, 64f);
            try
            {
                Assert.That(engine.RebuildDirty(), Is.EqualTo(1));
                Assert.That(engine.PageCount, Is.EqualTo(2));
                Assert.That(engine.PieceCount, Is.EqualTo(3));
                Assert.That(root.transform.childCount, Is.EqualTo(2));
                Physics.SyncTransforms();
                Assert.That(engine.TryPick(new Ray(a.Transform.Position + new Vector3(0f, 10f, 0f), Vector3.down),
                    20f, out PieceId picked), Is.True);
                Assert.That(picked, Is.EqualTo(c.Id), "The roof above the slab is the nearest hit.");
                PieceView isolated = engine.BeginEdit(c.Id);
                Assert.That(isolated.Id, Is.EqualTo(c.Id));
                Assert.That(engine.ExtractedCount, Is.EqualTo(1));
                Physics.SyncTransforms();
                Assert.That(engine.TryPick(new Ray(a.Transform.Position + new Vector3(0f, 10f, 0f), Vector3.down),
                    20f, out PieceId isolatedPick), Is.True);
                Assert.That(isolatedPick, Is.EqualTo(c.Id));
                engine.EndEdit(c.Id);
                Assert.That(engine.ExtractedCount, Is.Zero);
                Assert.That(engine.Contains(c.Id), Is.True);

                PieceData moved = a.WithTransform(new PieceTransform(a.Transform.Position + new Vector3(130f, 0f, 0f),
                    a.Transform.Rotation));
                Assert.That(history.Update(a.Id, moved).Changed, Is.True);
                Assert.That(engine.DirtyRegionCount, Is.EqualTo(2));
                Assert.That(engine.RebuildDirty(), Is.EqualTo(2));
                Assert.That(engine.OwnerOf(a.Id), Is.Not.EqualTo(engine.OwnerOf(b.Id)));
                Assert.That(world.TryGet(a.Id, out PieceData current) && current.Transform.Equals(moved.Transform), Is.True);

                PieceData resized = moved.WithDimensions(moved.Dimensions.Resize(0, moved.Dimensions.X + 1f));
                Assert.That(history.Update(a.Id, resized).Changed, Is.True);
                Assert.That(engine.DirtyRegionCount, Is.EqualTo(1));
                engine.RebuildDirty();
                PieceData recolored = resized.WithMaterial(LabMaterialIds.Brick);
                Assert.That(history.Update(a.Id, recolored).Changed, Is.True);
                engine.RebuildDirty();
                Assert.That(
                    world.TryGet(a.Id, out PieceData colored) &&
                    colored.MaterialId == LabMaterialIds.Brick,
                    Is.True);
                Assert.That(history.TryUndo(out _), Is.True);
                engine.RebuildDirty();
                Assert.That(history.Delete(a.Id).Changed, Is.True);
                engine.RebuildDirty();
                Assert.That(engine.Contains(a.Id), Is.False);
                Assert.That(history.TryUndo(out ConstructionChangeSet restored), Is.True);
                engine.RebuildDirty();
                Assert.That(engine.Contains(a.Id), Is.True);
                Assert.That(restored.After.Id, Is.EqualTo(a.Id));
                Assert.That(history.TryRedo(out ConstructionChangeSet deleted), Is.True);
                engine.RebuildDirty();
                Assert.That(engine.Contains(a.Id), Is.False);
                Assert.That(deleted.After, Is.Null);
                Assert.That(world.Count, Is.EqualTo(engine.PieceCount));
            }
            finally
            {
                engine.Dispose();
                Object.Destroy(root);
                Object.Destroy(material);
            }
            yield return null;
            Assert.That(engine.PageCount, Is.Zero);
            Assert.That(engine.RegionCount, Is.Zero);
        }
    }
}
