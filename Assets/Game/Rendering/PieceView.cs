using System;
using System.Collections.Generic;
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
        private PieceData geometryPiece;
        private MaterialRegistry materialRegistry;
        private bool materialAssigned;
        private Mesh ownedMesh;
        private readonly List<Mesh> finishMeshes=new List<Mesh>();
        private readonly List<GameObject> finishObjects=new List<GameObject>();
        private bool selectedState;
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
            if (NeedsMeshCollider(piece))
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
            if (ownedMesh == null || !SameGeometry(piece))
            {
                Mesh replacement = BlockMeshFactory.Build(piece.Type == PieceType.Wall && piece.Openings.Count > 0
                    ? WallOpeningGeometryGenerator.Generate(piece)
                    : piece.Dimensions.IsSlopedRoof
                    ? RoofGeometryGenerator.Generate(piece.Dimensions)
                    : piece.Dimensions.IsCurved ? CurvedGeometryGenerator.Generate(piece.Dimensions)
                    : piece.Dimensions.IsStair || piece.Dimensions.IsRamp
                        ? CirculationGeometryGenerator.Generate(piece.Dimensions)
                        : BlockGeometryGenerator.GeneratePiece(piece.Dimensions));
                meshFilter.sharedMesh = replacement;
                RefreshFinishes(piece);
                if (NeedsMeshCollider(piece))
                {
                    if (roofCollider == null) roofCollider = gameObject.AddComponent<MeshCollider>();
                    roofCollider.convex = false;
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
                geometryPiece = piece;
                boxCollider.center = new Vector3(0f, dimensions.Y * 0.5f, 0f);
                boxCollider.size = new Vector3(dimensions.X, dimensions.Y, dimensions.Z);
            }
            transform.SetPositionAndRotation(piece.Transform.Position, piece.Transform.Rotation);
            transform.localScale = Vector3.one;
        }

        private static bool NeedsMeshCollider(PieceData piece) =>
            piece.Dimensions.IsSlopedRoof || piece.Dimensions.IsStair || piece.Dimensions.IsRamp ||
            piece.Dimensions.IsCurved || piece.Type == PieceType.Wall && piece.Openings.Count > 0;

        private bool SameGeometry(PieceData piece)
        {
            if (!dimensions.Equals(piece.Dimensions) || geometryPiece.Openings.Count != piece.Openings.Count ||
                geometryPiece.Finishes.Count != piece.Finishes.Count) return false;
            for (int i=0;i<piece.Finishes.Count;i++)
                if (!geometryPiece.Finishes[i].Equals(piece.Finishes[i])) return false;
            for (int i = 0; i < piece.Openings.Count; i++)
            {
                WallOpening before = geometryPiece.Openings[i];
                WallOpening after = piece.Openings[i];
                if (before.Left != after.Left || before.Bottom != after.Bottom ||
                    before.Width != after.Width || before.Height != after.Height) return false;
            }
            return true;
        }

        private void RefreshFinishes(PieceData piece)
        {
            ClearFinishes();
            foreach (FinishSegment part in ArchitecturalFinishGeometry.Generate(piece))
            {
                var child=new GameObject("Linked finish "+part.FinishId);
                child.transform.SetParent(transform,false);
                Mesh mesh=BlockMeshFactory.Build(part.Geometry);
                child.AddComponent<MeshFilter>().sharedMesh=mesh;
                MeshRenderer renderer=child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=materialRegistry.Resolve(part.MaterialId);
                renderer.SetPropertyBlock(selectedState ? propertyBlock : null);
                finishMeshes.Add(mesh);finishObjects.Add(child);
            }
        }

        private void ClearFinishes()
        {
            foreach (GameObject child in finishObjects) if (child!=null) Destroy(child);
            foreach (Mesh mesh in finishMeshes) if (mesh!=null) Destroy(mesh);
            finishObjects.Clear();finishMeshes.Clear();
        }

        public void SetSelected(bool selected)
        {
            selectedState=selected;
            propertyBlock.SetColor("_BaseColor", selected ? new Color(1f, 0.75f, 0.25f) : Color.white);
            meshRenderer.SetPropertyBlock(selected ? propertyBlock : null);
            foreach (GameObject child in finishObjects)
                if (child!=null) child.GetComponent<MeshRenderer>().SetPropertyBlock(selected ? propertyBlock : null);
        }

        private void OnDestroy()
        {
            if (ownedMesh != null) Destroy(ownedMesh);
            ClearFinishes();
        }
    }
}
