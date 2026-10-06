using System;
using System.Collections.Generic;
using Aedifica.Construction;
using Aedifica.Geometry;
using UnityEngine;

namespace Aedifica.Rendering
{
    public sealed class ConstructionLabBlocks : MonoBehaviour
    {
        [SerializeField] private Material sharedBlockMaterial;

        private ConstructionWorld world;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<GameObject> views = new List<GameObject>();

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
                BlockGeometry geometry = BlockGeometryGenerator.Generate(piece.BlockDimensions);
                Mesh mesh = BlockMeshFactory.Build(geometry);
                meshes.Add(mesh);
                var view = new GameObject($"Lab Block {piece.Id}");
                views.Add(view);
                view.transform.SetParent(transform, false);
                view.transform.SetPositionAndRotation(piece.Transform.Position, piece.Transform.Rotation);
                view.transform.localScale = Vector3.one;
                view.AddComponent<MeshFilter>().sharedMesh = mesh;
                view.AddComponent<MeshRenderer>().sharedMaterial = sharedBlockMaterial;
            }
        }

        private void OnDestroy()
        {
            foreach (GameObject view in views) if (view != null) Destroy(view);
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
