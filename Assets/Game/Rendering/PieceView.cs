using System;
using Aedifica.Construction;
using Aedifica.Geometry;
using UnityEngine;

namespace Aedifica.Rendering
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public sealed class PieceView : MonoBehaviour
    {
        public PieceId Id { get; private set; }
        public MaterialId MaterialId { get; private set; }
        private PieceDimensions dimensions;
        private MaterialRegistry materialRegistry;
        private bool materialAssigned;
        private Mesh ownedMesh;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private BoxCollider boxCollider;
        private MeshCollider roofCollider;
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            boxCollider = GetComponent<BoxCollider>();
            propertyBlock = new MaterialPropertyBlock();
        }

        public void Initialize(PieceData piece, Material material)
            => Initialize(piece, new MaterialRegistry(material));

        public void Initialize(PieceData piece, MaterialRegistry registry)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (Id.IsValid) throw new InvalidOperationException("PieceView is already initialized.");
            Id = piece.Id;
            materialRegistry = registry;
            if (piece.Dimensions.IsSlopedRoof || piece.Dimensions.IsStair || piece.Dimensions.IsRamp)
            {
                roofCollider = GetComponent<MeshCollider>();
                if (roofCollider == null) roofCollider = gameObject.AddComponent<MeshCollider>();
            }
            Refresh(piece);
        }

        public void Refresh(PieceData piece)
        {
            if (piece == null || piece.Id != Id) throw new ArgumentException("View and piece IDs must match.", nameof(piece));
            if (!materialAssigned || MaterialId != piece.MaterialId)
            {
                meshRenderer.sharedMaterial = materialRegistry.Resolve(piece.MaterialId);
                MaterialId = piece.MaterialId;
                materialAssigned = true;
            }
            if (ownedMesh == null || !dimensions.Equals(piece.Dimensions))
            {
                Mesh replacement = BlockMeshFactory.Build(piece.Dimensions.IsSlopedRoof
                    ? RoofGeometryGenerator.Generate(piece.Dimensions)
                    : piece.Dimensions.IsStair || piece.Dimensions.IsRamp
                        ? CirculationGeometryGenerator.Generate(piece.Dimensions)
                        : BlockGeometryGenerator.GeneratePiece(piece.Dimensions));
                meshFilter.sharedMesh = replacement;
                if (piece.Dimensions.IsSlopedRoof || piece.Dimensions.IsStair || piece.Dimensions.IsRamp)
                {
                    roofCollider.sharedMesh = null;
                    roofCollider.sharedMesh = replacement;
                    roofCollider.enabled = true;
                    boxCollider.enabled = false;
                }
                else
                {
                    boxCollider.enabled = true;
                    if (roofCollider != null) roofCollider.enabled = false;
                }
                if (ownedMesh != null) Destroy(ownedMesh);
                ownedMesh = replacement;
                dimensions = piece.Dimensions;
                boxCollider.center = new Vector3(0f, dimensions.Y * 0.5f, 0f);
                boxCollider.size = new Vector3(dimensions.X, dimensions.Y, dimensions.Z);
            }
            transform.SetPositionAndRotation(piece.Transform.Position, piece.Transform.Rotation);
            transform.localScale = Vector3.one;
        }

        public void SetSelected(bool selected)
        {
            propertyBlock.SetColor("_BaseColor", selected ? new Color(1f, 0.75f, 0.25f) : Color.white);
            meshRenderer.SetPropertyBlock(selected ? propertyBlock : null);
        }

        private void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
        }
    }
}
