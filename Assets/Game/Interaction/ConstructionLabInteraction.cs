using Aedifica.Construction;
using Aedifica.Interaction.Camera;
using Aedifica.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aedifica.Interaction
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(ConstructionLabBlocks))]
    public sealed class ConstructionLabInteraction : MonoBehaviour
    {
        private const float ClickThresholdPixels = 5f;

        public static bool IsClick(Vector2 start, Vector2 end) =>
            (end - start).sqrMagnitude <= ClickThresholdPixels * ClickThresholdPixels;

        [SerializeField] private UnityEngine.Camera sceneCamera;
        [SerializeField] private CityBuilderCamera cityCamera;

        private ConstructionLabBlocks lab;
        private RuntimeGizmo gizmo;
        private readonly SelectionState selection = new SelectionState();
        private ManipulationMode mode;
        private ManipulationSession session;
        private PieceId? pressedPieceId;
        private Vector2 pressPosition;

        private void Awake() => lab = GetComponent<ConstructionLabBlocks>();

        private void Start()
        {
            var gizmoObject = new GameObject("Construction Gizmo");
            gizmo = gizmoObject.AddComponent<RuntimeGizmo>();
            gizmo.Initialize(sceneCamera, lab.SharedBlockMaterial);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && session == null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) mode = ManipulationMode.Move;
                if (keyboard.digit2Key.wasPressedThisFrame) mode = ManipulationMode.Rotate;
                if (keyboard.digit3Key.wasPressedThisFrame) mode = ManipulationMode.Resize;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) BeginPointer(pointer);
            if (session != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame)) UpdateManipulation(pointer);
            if (mouse.leftButton.wasReleasedThisFrame) EndPointer(pointer);

            if (selection.SelectedPieceId is PieceId id && lab.World.TryGet(id, out PieceData piece)) gizmo.Show(piece, mode);
            else gizmo.Hide();
        }

        private void BeginPointer(Vector2 pointer)
        {
            pressPosition = pointer;
            pressedPieceId = null;
            Physics.SyncTransforms();
            RaycastHit[] hits = Physics.RaycastAll(sceneCamera.ScreenPointToRay(pointer), 1000f);
            GizmoHandle nearestHandle = null;
            PieceView nearestPiece = null;
            float handleDistance = float.MaxValue;
            float pieceDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                GizmoHandle handle = hit.collider.GetComponent<GizmoHandle>();
                if (handle != null && handle.gameObject.activeInHierarchy && hit.distance < handleDistance)
                {
                    nearestHandle = handle;
                    handleDistance = hit.distance;
                }
                PieceView view = hit.collider.GetComponent<PieceView>();
                if (view != null && hit.distance < pieceDistance)
                {
                    nearestPiece = view;
                    pieceDistance = hit.distance;
                }
            }
            if (nearestHandle != null && (nearestPiece == null || handleDistance <= pieceDistance) &&
                selection.SelectedPieceId is PieceId selectedId && lab.World.TryGet(selectedId, out PieceData selectedPiece))
            {
                Vector3 axis = ManipulationSession.AxisVector(nearestHandle.Axis);
                if (nearestHandle.Mode == ManipulationMode.Resize) axis = selectedPiece.Transform.Rotation * axis;
                Vector3 origin = selectedPiece.Transform.Position;
                Vector3 screenDelta = sceneCamera.WorldToScreenPoint(origin + axis) - sceneCamera.WorldToScreenPoint(origin);
                Vector2 projected = new Vector2(screenDelta.x, screenDelta.y);
                float pixelsPerMeter = Mathf.Max(1f, projected.magnitude);
                session = new ManipulationSession(selectedPiece, nearestHandle.Mode, nearestHandle.Axis,
                    pointer, projected.normalized, pixelsPerMeter);
                cityCamera.SetPanSuppressed(true);
            }
            else if (nearestPiece != null) pressedPieceId = nearestPiece.Id;
        }

        private void UpdateManipulation(Vector2 pointer)
        {
            PieceData changed = session.Evaluate(pointer);
            if (!lab.World.TryGet(changed.Id, out PieceData current)) return;
            if (changed.Transform.Equals(current.Transform) && changed.BlockDimensions.Equals(current.BlockDimensions)) return;
            lab.Apply(changed);
        }

        private void EndPointer(Vector2 pointer)
        {
            if (session != null)
            {
                EndManipulation();
                return;
            }
            if (!IsClick(pressPosition, pointer)) return;
            PieceId? next = pressedPieceId;
            PieceId? old = selection.SelectedPieceId;
            if (old is PieceId oldId && lab.TryGetView(oldId, out PieceView oldView)) oldView.SetSelected(false);
            if (next is PieceId nextId && lab.World.TryGet(nextId, out _) && lab.TryGetView(nextId, out PieceView nextView))
            {
                selection.Select(nextId);
                nextView.SetSelected(true);
            }
            else selection.Clear();
        }

        private void OnDisable() => EndManipulation();

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) EndManipulation();
        }

        private void EndManipulation()
        {
            session = null;
            if (cityCamera != null) cityCamera.SetPanSuppressed(false);
        }

        private void OnDestroy()
        {
            if (gizmo != null) Destroy(gizmo.gameObject);
        }
    }
}
