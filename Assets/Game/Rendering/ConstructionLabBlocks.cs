using System;
using System.Collections.Generic;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Rendering
{
    public sealed class ConstructionLabBlocks : MonoBehaviour
    {
        [SerializeField] private Material sharedBlockMaterial;

        private ConstructionWorld world;
        private readonly Dictionary<PieceId, PieceView> views = new Dictionary<PieceId, PieceView>();

        public ConstructionWorld World => world;
        public Material SharedBlockMaterial => sharedBlockMaterial;

        private void Awake()
        {
            if (sharedBlockMaterial == null) throw new InvalidOperationException("ConstructionLab requires a URP block material.");
            world = new ConstructionWorld();
            world.Add(new PieceData(PieceId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                new PieceTransform(new Vector3(-5f, 0f, -4f), Quaternion.identity), new BlockDimensions(2f, 1f, 3f)));
            world.Add(new PieceData(PieceId.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
                new PieceTransform(new Vector3(0f, 0f, -4f), Quaternion.Euler(0f, 30f, 0f)), new BlockDimensions(1f, 3f, 1f)));
            world.Add(new PieceData(PieceId.Parse("cccccccccccccccccccccccccccccccc"),
                new PieceTransform(new Vector3(5f, 0f, -4f), Quaternion.identity), new BlockDimensions(4f, 0.5f, 2f)));

            foreach (PieceData piece in world.Pieces)
            {
                var viewObject = new GameObject($"Lab Block {piece.Id}");
                viewObject.transform.SetParent(transform, false);
                PieceView view = viewObject.AddComponent<PieceView>();
                view.Initialize(piece, sharedBlockMaterial);
                views.Add(piece.Id, view);
            }
        }

        private void OnDestroy()
        {
            foreach (PieceView view in views.Values) if (view != null) Destroy(view.gameObject);
        }

        public bool TryGetView(PieceId id, out PieceView view) => views.TryGetValue(id, out view);

        public bool Apply(PieceData replacement)
        {
            if (replacement == null || !world.Replace(replacement.Id, replacement)) return false;
            views[replacement.Id].Refresh(replacement);
            return true;
        }
    }
}
