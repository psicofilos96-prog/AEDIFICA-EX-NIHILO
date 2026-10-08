using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    public sealed class ConstructionLabBlocks : MonoBehaviour
    {
        [SerializeField] private Material sharedBlockMaterial;
        [SerializeField] private Material stoneMaterial;
        [SerializeField] private Material brickMaterial;
        [SerializeField] private Material plasterMaterial;
        [SerializeField] private bool logMissingMaterials;
        [SerializeField] private int historyCapacity = 256;

        private ConstructionWorld world;
        private ConstructionCommandHistory history;
        private readonly Dictionary<PieceId, PieceView> views = new Dictionary<PieceId, PieceView>();

        public ConstructionWorld World => world;
        public Material SharedBlockMaterial => sharedBlockMaterial;
        public MaterialRegistry Registry { get; private set; }

        public void ConfigureMaterial(Material material)
            => ConfigureMaterials(material, null, null, null);

        public void ConfigureMaterials(Material neutral, Material stone, Material brick, Material plaster)
        {
            if (world != null) throw new InvalidOperationException("Configure the lab before Awake.");
            sharedBlockMaterial = neutral != null ? neutral : throw new ArgumentNullException(nameof(neutral));
            stoneMaterial = stone;
            brickMaterial = brick;
            plasterMaterial = plaster;
        }

        private void Awake()
        {
            if (sharedBlockMaterial == null) throw new InvalidOperationException("ConstructionLab requires a URP block material.");
            Registry = new MaterialRegistry(sharedBlockMaterial, stoneMaterial, brickMaterial, plasterMaterial, logMissingMaterials);
            world = new ConstructionWorld();
            world.Add(new PieceData(PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                new PieceTransform(new Vector3(-5f, 0f, -4f), Quaternion.identity), new BlockDimensions(2f, 1f, 3f)));
            world.Add(new PieceData(PieceId.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
                new PieceTransform(new Vector3(0f, 0f, -4f), Quaternion.Euler(0f, 30f, 0f)), new BlockDimensions(1f, 3f, 1f)));
            world.Add(new PieceData(PieceId.Parse("cccccccccccccccccccccccccccccccc"),
                new PieceTransform(new Vector3(5f, 0f, -4f), Quaternion.identity), new BlockDimensions(4f, 0.5f, 2f)));
            world.Add(new PieceData(PieceId.Parse("dddddddddddddddddddddddddddddddd"),
                new PieceTransform(new Vector3(0f, 0f, 6f), Quaternion.identity), new SlabDimensions(8f, 0.2f, 6f)).WithMaterial(LabMaterialIds.Plaster));
            world.Add(new PieceData(PieceId.Parse("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"),
                new PieceTransform(new Vector3(0f, 0.2f, 8.7f), Quaternion.identity), new WallDimensions(8f, 3f, 0.3f)).WithMaterial(LabMaterialIds.Stone));
            world.Add(new PieceData(PieceId.Parse("ffffffffffffffffffffffffffffffff"),
                new PieceTransform(new Vector3(-3.7f, 0.2f, 6f), Quaternion.Euler(0f, 90f, 0f)), new WallDimensions(5.4f, 2.5f, 0.25f)).WithMaterial(LabMaterialIds.Brick));
            world.Add(new PieceData(PieceId.Parse("10000000000000000000000000000001"),
                new PieceTransform(new Vector3(-3f, 0f, -13f), Quaternion.identity), new ColumnDimensions(0.7f, 3f, 0.7f)).WithMaterial(LabMaterialIds.Stone));
            world.Add(new PieceData(PieceId.Parse("10000000000000000000000000000002"),
                new PieceTransform(new Vector3(3f, 0f, -13f), Quaternion.identity), new ColumnDimensions(0.7f, 3f, 0.7f)).WithMaterial(LabMaterialIds.Stone));
            world.Add(new PieceData(PieceId.Parse("10000000000000000000000000000003"),
                new PieceTransform(new Vector3(0f, 3f, -13f), Quaternion.identity), new BeamDimensions(6.7f, 0.5f, 0.55f)).WithMaterial(LabMaterialIds.Brick));
            world.Add(new PieceData(PieceId.Parse("10000000000000000000000000000004"),
                new PieceTransform(new Vector3(0f, 0.2f, 3f), Quaternion.identity), new ParapetDimensions(8f, 0.9f, 0.3f)).WithMaterial(LabMaterialIds.Stone));
            world.Add(new PieceData(PieceId.Parse("20000000000000000000000000000001"),
                new PieceTransform(new Vector3(-12f, 0f, 17f), Quaternion.identity), new FlatRoofDimensions(4f, 3f, 0.25f)).WithMaterial(LabMaterialIds.Plaster));
            world.Add(new PieceData(PieceId.Parse("20000000000000000000000000000002"),
                new PieceTransform(new Vector3(-4f, 0f, 17f), Quaternion.identity), new ShedRoofDimensions(4f, 3f, 0.25f, 1f)).WithMaterial(LabMaterialIds.Stone));
            world.Add(new PieceData(PieceId.Parse("20000000000000000000000000000003"),
                new PieceTransform(new Vector3(4f, 0f, 17f), Quaternion.identity), new GableRoofDimensions(4f, 3f, 0.25f, 1.2f)).WithMaterial(LabMaterialIds.Brick));
            world.Add(new PieceData(PieceId.Parse("20000000000000000000000000000004"),
                new PieceTransform(new Vector3(12f, 0f, 17f), Quaternion.identity), new HipRoofDimensions(4f, 3f, 0.25f, 1.2f)).WithMaterial(LabMaterialIds.Plaster));
            world.Add(new PieceData(PieceId.Parse("40000000000000000000000000000001"),
                new PieceTransform(new Vector3(-8f, 0f, -22f), Quaternion.identity), new StairDimensions(2f, 2f, 3f, 10)).WithMaterial(LabMaterialIds.Stone));
            world.Add(new PieceData(PieceId.Parse("40000000000000000000000000000002"),
                new PieceTransform(new Vector3(8f, 0f, -22f), Quaternion.identity), new RampDimensions(2f, 2f, 4f, 0.2f)).WithMaterial(LabMaterialIds.Brick));
            world.Add(new PieceData(PieceId.Parse("50000000000000000000000000000001"),
                new PieceTransform(new Vector3(-12f, 0f, 27f), Quaternion.identity),
                new ArchDimensions(4f, 3f, 0.7f, 0.6f, 1.2f, 0.25f)).WithMaterial(LabMaterialIds.Stone));
            world.Add(new PieceData(PieceId.Parse("50000000000000000000000000000002"),
                new PieceTransform(new Vector3(0f, 0f, 27f), Quaternion.identity),
                new VaultDimensions(4f, 2.8f, 5f, 0.25f)).WithMaterial(LabMaterialIds.Brick));
            world.Add(new PieceData(PieceId.Parse("50000000000000000000000000000003"),
                new PieceTransform(new Vector3(12f, 0f, 27f), Quaternion.identity),
                new DomeDimensions(4f, 2.5f, 0.25f)).WithMaterial(LabMaterialIds.Plaster));

            foreach (PieceData piece in world.Pieces)
            {
                var viewObject = new GameObject($"Lab {piece.Type} {piece.Id}");
                viewObject.transform.SetParent(transform, false);
                PieceView view = viewObject.AddComponent<PieceView>();
                view.Initialize(piece, Registry);
                views.Add(piece.Id, view);
            }
            history = new ConstructionCommandHistory(world, historyCapacity > 0 ? historyCapacity : 256);
        }

        private void OnDestroy()
        {
            foreach (PieceView view in views.Values) if (view != null) Destroy(view.gameObject);
        }

        public bool TryGetView(PieceId id, out PieceView view) => views.TryGetValue(id, out view);

        public bool Add(PieceData piece)
        {
            if (piece == null || world.TryGet(piece.Id, out _) || views.ContainsKey(piece.Id)) return false;
            var viewObject = new GameObject($"Lab {piece.Type} {piece.Id}");
            viewObject.transform.SetParent(transform, false);
            try
            {
                PieceView view = viewObject.AddComponent<PieceView>();
                view.Initialize(piece, Registry);
                if (!history.Create(piece).Changed) { Destroy(viewObject); return false; }
                views.Add(piece.Id, view);
                return true;
            }
            catch
            {
                Destroy(viewObject);
                throw;
            }
        }

        public bool Apply(PieceData replacement)
        {
            if (replacement == null) return false;
            ConstructionChangeSet change = history.Update(replacement.Id, replacement);
            if (change.Status == ConstructionChangeStatus.Rejected) return false;
            if (change.Changed) views[replacement.Id].Refresh(replacement);
            return true;
        }

        public bool Delete(PieceId id)
        {
            if (!history.Delete(id).Changed) return false;
            if (views.TryGetValue(id, out PieceView view))
            {
                views.Remove(id);
                if (view != null) Destroy(view.gameObject);
            }
            return true;
        }

        public bool Undo()
        {
            if (!history.TryUndo(out ConstructionChangeSet change)) return false;
            SyncView(change);
            return true;
        }

        public bool Redo()
        {
            if (!history.TryRedo(out ConstructionChangeSet change)) return false;
            SyncView(change);
            return true;
        }

        private void SyncView(ConstructionChangeSet change)
        {
            if (change.After == null)
            {
                if (views.TryGetValue(change.PieceId, out PieceView removed))
                {
                    views.Remove(change.PieceId);
                    if (removed != null) Destroy(removed.gameObject);
                }
                return;
            }
            if (views.TryGetValue(change.PieceId, out PieceView existing))
            {
                existing.Refresh(change.After);
                return;
            }
            var visual = new GameObject($"Lab {change.After.Type} {change.PieceId}");
            visual.transform.SetParent(transform, false);
            PieceView view = visual.AddComponent<PieceView>();
            view.Initialize(change.After, Registry);
            views.Add(change.PieceId, view);
        }

        public bool CycleMaterial(PieceId id)
        {
            if (!world.TryGet(id, out PieceData piece)) return false;
            return Apply(piece.WithMaterial(Registry.Next(piece.MaterialId)));
        }
    }
}
