using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class FreePieceCatalogTests
    {
        [Test]
        public void EveryCatalogEntryCreatesValidIndependentDomainPiece()
        {
            var types = new HashSet<PieceType>();
            var ids = new HashSet<PieceId>();
            foreach (PieceType type in FreePieceCatalog.Types)
            {
                Assert.That(types.Add(type), Is.True, $"Duplicate catalog entry {type}");
                PieceId id = PieceIdGenerator.New();
                Assert.That(ids.Add(id), Is.True);
                PieceData piece = FreePieceCatalog.Create(type, id, new Vector3(7f, 1.25f, -9f), 45f);
                Assert.That(piece.Id, Is.EqualTo(id));
                Assert.That(piece.Type, Is.EqualTo(type));
                Assert.That(piece.Dimensions.IsValid, Is.True);
                Assert.That(piece.Transform.IsValid, Is.True);
                Assert.That(piece.Transform.Position, Is.EqualTo(new Vector3(7f, 1.25f, -9f)));
                Assert.That(Quaternion.Angle(piece.Transform.Rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.01f));
            }
            Assert.That(types.Contains(PieceType.Wall), Is.True);
            Assert.That(types.Count, Is.EqualTo(15));
        }

        [Test]
        public void CatalogRejectsUnsupportedTypesAndInvalidTransforms()
        {
            PieceId id = PieceIdGenerator.New();
            Assert.Throws<ArgumentOutOfRangeException>(() => FreePieceCatalog.Create(PieceType.Unknown, id, Vector3.zero, 0f));
            Assert.Throws<ArgumentException>(() => FreePieceCatalog.Create(PieceType.Wall, id,
                new Vector3(float.NaN, 0f, 0f), 0f));
        }
    }
}
