using System;
using Aedifica.Construction;
using Aedifica.Geometry;
using Aedifica.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aedifica.Interaction
{
    // Owned by ConstructionLabInteraction: no second Update or selection system.
    public sealed class FreePiecePlacement
    {
        private const float MaximumRayDistance = 500f;
        private const float ToolbarWidth = 370f;
        private readonly ConstructionLabBlocks lab;
        private readonly UnityEngine.Camera camera;
        private readonly SnapSettings snapping;
        private readonly SnapResolver resolver = new SnapResolver();
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private GameObject preview;
        private Mesh previewMesh;
        private MeshRenderer previewRenderer;
        private MaterialPropertyBlock previewTint;
        private PieceId candidateId;
        private PieceType type;
        private Vector3 position;
        private float yaw;
        private bool valid;
        private bool awaitingPointerMove;
        private Vector2 lastPointer;
        private string feedback;

        public bool Active => preview != null;
        public bool Valid => valid;
        public PieceType Type => type;
        public Vector3 Position => position;
        public GameObject PreviewObject => preview;
        public string Feedback => feedback;

        public FreePiecePlacement(ConstructionLabBlocks lab, UnityEngine.Camera camera, SnapSettings snapping)
        {
            this.lab = lab ?? throw new ArgumentNullException(nameof(lab));
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.snapping = snapping ?? throw new ArgumentNullException(nameof(snapping));
        }

        public void Begin(PieceType requested)
        {
            Cancel();
            type = requested;
            candidateId = PieceIdGenerator.New(); // transient snap candidate; never added to the world
            PieceData template = FreePieceCatalog.Create(type, candidateId, Vector3.zero, 0f);
            BlockGeometry geometry = template.Dimensions.IsSlopedRoof
                ? RoofGeometryGenerator.Generate(template.Dimensions)
                : template.Dimensions.IsCurved ? CurvedGeometryGenerator.Generate(template.Dimensions)
                : template.Dimensions.IsStair || template.Dimensions.IsRamp
                    ? CirculationGeometryGenerator.Generate(template.Dimensions)
                    : BlockGeometryGenerator.GeneratePiece(template.Dimensions);
            previewMesh = BlockMeshFactory.Build(geometry);
            preview = new GameObject("Placement Preview (not a piece)");
            MeshFilter filter = preview.AddComponent<MeshFilter>();
            filter.sharedMesh = previewMesh;
            previewRenderer = preview.AddComponent<MeshRenderer>();
            previewRenderer.sharedMaterial = lab.Registry.Resolve(template.MaterialId);
            previewTint = new MaterialPropertyBlock();
            previewTint.SetColor("_BaseColor", new Color(0.4f, 1f, 0.6f, 1f));
            previewRenderer.SetPropertyBlock(previewTint);
            preview.SetActive(false);
            yaw = 0f;
            valid = false;
            awaitingPointerMove = false;
            feedback = "Mova o cursor para posicionar.";
        }

        public void Cancel()
        {
            resolver.Reset();
            valid = false;
            awaitingPointerMove = false;
            if (preview != null) UnityEngine.Object.Destroy(preview);
            if (previewMesh != null) UnityEngine.Object.Destroy(previewMesh);
            preview = null;
            previewMesh = null;
        }

        public bool UpdatePosition(Vector2 pointer)
        {
            if (!Active) return false;
            if (awaitingPointerMove && (pointer - lastPointer).sqrMagnitude < 1f)
            {
                valid = false;
                preview.SetActive(false);
                return false;
            }
            awaitingPointerMove = false;
            lastPointer = pointer;
            valid = TryPosition(pointer, out Vector3 proposed);
            if (valid)
            {
                if (snapping.PositionSnapEnabled)
                {
                    proposed.x = SnapPolicy.Quantize(proposed.x, snapping.PositionIncrement);
                    proposed.z = SnapPolicy.Quantize(proposed.z, snapping.PositionIncrement);
                }
                PieceData candidate = FreePieceCatalog.Create(type, candidateId, proposed, yaw);
                if (snapping.HasGeometricSnap)
                {
                    var moveSession = new ManipulationSession(candidate, ManipulationMode.Move, ManipulationAxis.X,
                        Vector2.zero, Vector2.right, 1f);
                    candidate = resolver.Resolve(candidate, moveSession, snapping, lab.World.Pieces);
                }
                else resolver.Reset();
                position = candidate.Transform.Position;
                preview.transform.SetPositionAndRotation(position, candidate.Transform.Rotation);
                preview.SetActive(true);
                feedback = "Pronto para colocar.";
            }
            else
            {
                preview.SetActive(false);
                feedback = "Sem superfície ou plano de construção a até 500 m sob o cursor.";
            }
            return valid;
        }

        private bool TryPosition(Vector2 pointer, out Vector3 result)
        {
            result = default;
            if (float.IsNaN(pointer.x) || float.IsNaN(pointer.y) || float.IsInfinity(pointer.x) || float.IsInfinity(pointer.y) ||
                !camera.pixelRect.Contains(pointer)) return false;
            Ray ray = camera.ScreenPointToRay(pointer);
            float closest = MaximumRayDistance;
            bool surfaceFound = false;
            int count = Physics.RaycastNonAlloc(ray, hits, MaximumRayDistance);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.distance >= closest || hit.normal.y < 0.5f ||
                    hit.collider.GetComponent<GizmoHandle>() != null) continue;
                closest = hit.distance;
                result = hit.point + Vector3.up * 0.001f;
                surfaceFound = true;
            }
            if (!surfaceFound)
            {
                Plane plane = new Plane(Vector3.up, Vector3.zero);
                if (!plane.Raycast(ray, out float enter) || enter <= 0f || enter > MaximumRayDistance) return false;
                result = ray.GetPoint(enter);
            }
            return IsFinite(result.x) && IsFinite(result.y) && IsFinite(result.z);
        }

        public void Rotate(int direction)
        {
            if (!Active || direction == 0) return;
            yaw += direction * (snapping.RotationSnapEnabled ? snapping.RotationIncrementDegrees : 15f);
            yaw = Mathf.Repeat(yaw, 360f);
            if (preview.activeSelf) preview.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public bool Confirm(out PieceData created)
        {
            created = null;
            if (!Active || !valid) return false;
            created = FreePieceCatalog.Create(type, PieceIdGenerator.New(), position, yaw);
            if (!lab.Add(created))
            {
                created = null;
                feedback = "Não foi possível registrar a peça no mundo.";
                return false;
            }
            feedback = $"{type} criada. Clique novamente para continuar ou Esc para sair.";
            awaitingPointerMove = true;
            valid = false;
            preview.SetActive(false);
            return true;
        }

        public bool IsToolbarPoint(Vector2 screenPoint) => screenPoint.x >= 12f &&
            screenPoint.x <= ToolbarWidth - 12f &&
            screenPoint.y >= Screen.height - 680f && screenPoint.y <= Screen.height - 100f;

        public void DrawGUI(Action<PieceType> selectType)
        {
            GUILayout.BeginArea(new Rect(12f, 100f, ToolbarWidth - 24f, 580f), GUI.skin.box);
            GUILayout.Label("CRIAR PEÇA — escolha um tipo");
            for (int i = 0; i < FreePieceCatalog.Types.Length; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 3, FreePieceCatalog.Types.Length); j++)
                {
                    PieceType option = FreePieceCatalog.Types[j];
                    if (GUILayout.Button(option.ToString().Replace("Roof", " Roof"), GUILayout.Height(30f))) selectType(option);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(Active
                ? $"{type}: LMB colocar | Esc cancelar | Z/X girar (15°)\n{feedback}\nGrid G: {(snapping.PositionSnapEnabled ? "ON" : "OFF")} | Angle R: {(snapping.RotationSnapEnabled ? "ON" : "OFF")} | T/H/P: Surface/Edge/Endpoint"
                : "Selecione uma peça acima. Fora da criação: LMB seleciona ou arrasta o cenário.");
            GUILayout.EndArea();
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
