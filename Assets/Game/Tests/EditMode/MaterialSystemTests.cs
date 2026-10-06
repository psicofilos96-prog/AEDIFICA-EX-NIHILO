using System;
using Aedifica.Construction;
using Aedifica.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class MaterialSystemTests
    {
        private static readonly PieceId Id = PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        private static readonly PieceTransform Transform = new PieceTransform(Vector3.zero, Quaternion.identity);

        [Test]
        public void MaterialIdIsStableComparableAndRejectsInvalidValues()
        {
            Assert.That(LabMaterialIds.Neutral.IsValid, Is.True);
            Assert.That(LabMaterialIds.Neutral, Is.EqualTo(MaterialId.Parse(LabMaterialIds.Neutral.ToString().ToUpperInvariant())));
            Assert.That(LabMaterialIds.Neutral.GetHashCode(), Is.EqualTo(MaterialId.Parse(LabMaterialIds.Neutral.ToString()).GetHashCode()));
            Assert.That(LabMaterialIds.Neutral, Is.Not.EqualTo(LabMaterialIds.Stone));
            Assert.That(default(MaterialId).IsValid, Is.False);
            Assert.That(MaterialId.TryParse(Guid.Empty.ToString(), out _), Is.False);
            Assert.Throws<ArgumentException>(() => MaterialId.Parse("not-a-guid"));
        }

        [Test]
        public void PieceCopiesPreserveMaterialUntilWithMaterial()
        {
            var piece = new PieceData(Id, Transform, new WallDimensions(4f, 3f, 0.2f)).WithMaterial(LabMaterialIds.Stone);
            Assert.That(piece.MaterialId, Is.EqualTo(LabMaterialIds.Stone));
            Assert.That(piece.WithTransform(new PieceTransform(Vector3.right, Quaternion.identity)).MaterialId, Is.EqualTo(LabMaterialIds.Stone));
            Assert.That(piece.WithWallDimensions(new WallDimensions(5f, 3f, 0.2f)).MaterialId, Is.EqualTo(LabMaterialIds.Stone));
            PieceData changed = piece.WithMaterial(LabMaterialIds.Brick);
            Assert.That(changed.MaterialId, Is.EqualTo(LabMaterialIds.Brick));
            Assert.That(changed.Id, Is.EqualTo(piece.Id));
            Assert.That(changed.Type, Is.EqualTo(piece.Type));
            Assert.That(changed.Transform, Is.EqualTo(piece.Transform));
            Assert.That(changed.Dimensions, Is.EqualTo(piece.Dimensions));
            Assert.That(piece.MaterialId, Is.EqualTo(LabMaterialIds.Stone));
        }

        [TestCase(PieceType.Block)]
        [TestCase(PieceType.Slab)]
        public void OtherPieceFamiliesPreserveMaterialAcrossDimensionChanges(PieceType type)
        {
            PieceData piece = type == PieceType.Block
                ? new PieceData(Id, Transform, new BlockDimensions(2f, 1f, 3f))
                : new PieceData(Id, Transform, new SlabDimensions(2f, 0.2f, 3f));
            piece = piece.WithMaterial(LabMaterialIds.Plaster);
            PieceData resized = piece.WithDimensions(piece.Dimensions.Resize(0, 4f));
            Assert.That(resized.MaterialId, Is.EqualTo(LabMaterialIds.Plaster));
            Assert.That(resized.Type, Is.EqualTo(type));
        }

        [Test]
        public void RegistryResolvesKnownIdsAndFallsBackForUnknownOrAbsentId()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var neutral = new Material(shader);
            var stone = new Material(shader);
            try
            {
                var registry = new MaterialRegistry(neutral, stone);
                Assert.That(registry.TryGet(LabMaterialIds.Neutral, out Material found), Is.True);
                Assert.That(found, Is.SameAs(neutral));
                Assert.That(registry.Resolve(LabMaterialIds.Stone), Is.SameAs(stone));
                Assert.That(registry.Resolve(LabMaterialIds.Brick), Is.SameAs(neutral));
                Assert.That(registry.Resolve(default), Is.SameAs(neutral));
                Assert.That(registry.Next(LabMaterialIds.Neutral), Is.EqualTo(LabMaterialIds.Stone));
                Assert.That(registry.Next(LabMaterialIds.Stone), Is.EqualTo(LabMaterialIds.Neutral));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(neutral);
                UnityEngine.Object.DestroyImmediate(stone);
            }
        }
    }
}
