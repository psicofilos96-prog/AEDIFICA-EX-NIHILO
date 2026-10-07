using System;
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
        [SerializeField] private bool debugSelection;
        [SerializeField] private SnapSettings snapSettings = new SnapSettings();

        public PieceId? SelectedPieceId => selection.SelectedPieceId;
        public ManipulationMode Mode => mode;
        public SnapSettings Snapping => snapSettings;

        public void Configure(UnityEngine.Camera camera, CityBuilderCamera controller)
        {
            if (lab != null) throw new InvalidOperationException("Configure interaction before Awake.");
            sceneCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            cityCamera = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
        }

        private ConstructionLabBlocks lab;
        private RuntimeGizmo gizmo;
        private readonly SelectionState selection = new SelectionState();
        private ManipulationMode mode;
        private ResizeMode resizeMode;
        private ManipulationSession session;
        private readonly SnapResolver geometricSnap = new SnapResolver();
        private PieceId? pressedPieceId;
        private Vector2 pressPosition;
        private bool draggingPan;
        private bool lastObservedLeftPressed;

        private void Awake()
        {
            lab = GetComponent<ConstructionLabBlocks>();
            snapSettings ??= new SnapSettings();
            if (lab == null) throw new InvalidOperationException("ConstructionLabInteraction requires ConstructionLabBlocks on the same GameObject.");
            if (sceneCamera == null) throw new InvalidOperationException("ConstructionLabInteraction.sceneCamera is not assigned.");
            if (cityCamera == null) throw new InvalidOperationException("ConstructionLabInteraction.cityCamera is not assigned.");
            if (cityCamera.GetComponent<UnityEngine.Camera>() != sceneCamera)
                throw new InvalidOperationException("ConstructionLabInteraction cameras must refer to the same Camera GameObject.");
            if (debugSelection) Debug.Log($"Selection Awake: lab={lab.name}, camera={sceneCamera.name}, cityCamera={cityCamera.name}", this);
        }

        private void Start()
        {
            var gizmoObject = new GameObject("Construction Gizmo");
            gizmo = gizmoObject.AddComponent<RuntimeGizmo>();
            gizmo.Initialize(sceneCamera, lab.SharedBlockMaterial);
            if (debugSelection) Debug.Log($"Selection Start: pieces={lab.World.Count}, mouse={(Mouse.current != null ? Mouse.current.name : "none")}", this);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && session == null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) mode = ManipulationMode.Move;
                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) mode = ManipulationMode.Rotate;
                if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) mode = ManipulationMode.Resize;
                if (keyboard.fKey.wasPressedThisFrame)
                {
                    resizeMode = resizeMode == ResizeMode.Center ? ResizeMode.Face : ResizeMode.Center;
                    Debug.Log($"Resize mode: {resizeMode}", this);
                }
                if (keyboard.mKey.wasPressedThisFrame && selection.SelectedPieceId is PieceId selectedMaterialPiece)
                    lab.CycleMaterial(selectedMaterialPiece);
                if (keyboard.gKey.wasPressedThisFrame) TogglePositionSnap();
                if (keyboard.rKey.wasPressedThisFrame) ToggleRotationSnap();
                if (keyboard.tKey.wasPressedThisFrame) ToggleGeometricSnap(GeometricSnapKind.Surface);
                if (keyboard.hKey.wasPressedThisFrame) ToggleGeometricSnap(GeometricSnapKind.Edge);
                if (keyboard.pKey.wasPressedThisFrame) ToggleGeometricSnap(GeometricSnapKind.Endpoint);
                if (keyboard.pageUpKey.wasPressedThisFrame) AdjustSelectedRoofRise(0.1f);
                if (keyboard.pageDownKey.wasPressedThisFrame) AdjustSelectedRoofRise(-0.1f);
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            bool cursorUnavailableForPan = mouse.rightButton.isPressed || Cursor.lockState == CursorLockMode.Locked;
            if (cursorUnavailableForPan && draggingPan)
            {
                cityCamera.EndPan();
                draggingPan = false;
            }
            Vector2 pointer = mouse.position.ReadValue();
            if (debugSelection && mouse.leftButton.isPressed != lastObservedLeftPressed)
                Debug.Log($"Selection LMB state: pressed={mouse.leftButton.isPressed}, wasPressed={mouse.leftButton.wasPressedThisFrame}, wasReleased={mouse.leftButton.wasReleasedThisFrame}, pointer={pointer}", this);
            lastObservedLeftPressed = mouse.leftButton.isPressed;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (debugSelection) Debug.Log($"Selection PointerDown: {pointer}", this);
                PointerDown(pointer);
            }
            if (session != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame)) UpdateManipulation(pointer);
            else if (mouse.leftButton.isPressed && !cursorUnavailableForPan && !IsClick(pressPosition, pointer))
            {
                if (!draggingPan)
                {
                    draggingPan = cityCamera.BeginPan();
                }
            }
            if (mouse.leftButton.wasReleasedThisFrame) PointerUp(pointer);

            if (selection.SelectedPieceId is PieceId id && lab.World.TryGet(id, out PieceData piece)) gizmo.Show(piece, mode, resizeMode);
            else gizmo.Hide();
        }

        public void PointerDown(Vector2 pointer)
        {
            pressPosition = pointer;
            draggingPan = false;
            pressedPieceId = null;
            PickResult picked = Pick(pointer);
            GizmoHandle nearestHandle = picked.Handle;
            PieceView nearestPiece = picked.View;
            float handleDistance = picked.HandleDistance;
            float pieceDistance = picked.ViewDistance;
            if (nearestHandle != null && (nearestPiece == null || handleDistance <= pieceDistance) &&
                selection.SelectedPieceId is PieceId selectedId && lab.World.TryGet(selectedId, out PieceData selectedPiece))
            {
                Vector3 axis = ManipulationSession.AxisVector(nearestHandle.Axis);
                if (nearestHandle.Mode == ManipulationMode.Resize)
                    axis = selectedPiece.Transform.Rotation * axis * (resizeMode == ResizeMode.Face ? nearestHandle.FaceSign : 1);
                Vector3 origin = selectedPiece.Transform.Position;
                Vector3 screenDelta = sceneCamera.WorldToScreenPoint(origin + axis) - sceneCamera.WorldToScreenPoint(origin);
                Vector2 projected = new Vector2(screenDelta.x, screenDelta.y);
                float pixelsPerMeter = Mathf.Max(1f, projected.magnitude);
                session = new ManipulationSession(selectedPiece, nearestHandle.Mode, nearestHandle.Axis,
                    pointer, projected.normalized, pixelsPerMeter, snapSettings, resizeMode, nearestHandle.FaceSign);
                geometricSnap.Reset();
                cityCamera.SetPanSuppressed(true);
            }
            else if (nearestPiece != null) pressedPieceId = nearestPiece.Id;
            if (debugSelection) Debug.Log($"Selection PointerDown result: pressedPiece={pressedPieceId}, handle={nearestHandle}", this);
        }

        private void TogglePositionSnap()
        {
            try { Debug.Log($"Grid Snap: {(snapSettings.TogglePosition() ? "ON" : "OFF")} ({snapSettings.PositionIncrement} m)", this); }
            catch (ArgumentOutOfRangeException exception) { Debug.LogError(exception.Message, this); }
        }

        private void ToggleRotationSnap()
        {
            try { Debug.Log($"Angle Snap: {(snapSettings.ToggleRotation() ? "ON" : "OFF")} ({snapSettings.RotationIncrementDegrees}°)", this); }
            catch (ArgumentOutOfRangeException exception) { Debug.LogError(exception.Message, this); }
        }

        private void ToggleGeometricSnap(GeometricSnapKind kind)
        {
            try
            {
                snapSettings.ValidateGeometric();
                bool enabled;
                if (kind == GeometricSnapKind.Surface) enabled = snapSettings.SurfaceSnapEnabled = !snapSettings.SurfaceSnapEnabled;
                else if (kind == GeometricSnapKind.Edge) enabled = snapSettings.EdgeSnapEnabled = !snapSettings.EdgeSnapEnabled;
                else enabled = snapSettings.EndpointSnapEnabled = !snapSettings.EndpointSnapEnabled;
                Debug.Log($"{kind} Snap: {(enabled ? "ON" : "OFF")}", this);
            }
            catch (ArgumentOutOfRangeException exception) { Debug.LogError(exception.Message, this); }
        }

        private void AdjustSelectedRoofRise(float step)
        {
            if (!(selection.SelectedPieceId is PieceId id) || !lab.World.TryGet(id, out PieceData piece) ||
                !piece.Dimensions.IsSlopedRoof) return;
            float rise = Mathf.Max(ManipulationSession.MinimumDimension, piece.Dimensions.Rise + step);
            PieceData replacement = piece.WithRise(rise);
            if (lab.Apply(replacement)) Debug.Log($"Roof Rise: {rise:F2} m", this);
        }

        public bool TryPickPieceAt(Vector2 pointer, out PieceId id)
        {
            PieceView view = Pick(pointer).View;
            if (view != null && lab.World.TryGet(view.Id, out _))
            {
                id = view.Id;
                return true;
            }
            id = default;
            return false;
        }

        private PickResult Pick(Vector2 pointer)
        {
            Physics.SyncTransforms();
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
            if (debugSelection) Debug.Log($"Selection Ray: origin={ray.origin}, direction={ray.direction}, hits={hits.Length}", this);
            if (debugSelection && hits.Length == 0) LogMissDiagnostics(pointer, ray);
            var result = new PickResult { HandleDistance = float.MaxValue, ViewDistance = float.MaxValue };
            foreach (RaycastHit hit in hits)
            {
                GizmoHandle handle = hit.collider.GetComponent<GizmoHandle>();
                PieceView view = hit.collider.GetComponent<PieceView>();
                if (debugSelection) Debug.Log($"Selection Hit: collider={hit.collider.name}, distance={hit.distance}, view={view}, id={(view != null ? view.Id.ToString() : "none")}", this);
                if (handle != null && handle.gameObject.activeInHierarchy && hit.distance < result.HandleDistance)
                {
                    result.Handle = handle;
                    result.HandleDistance = hit.distance;
                }
                if (view != null && hit.distance < result.ViewDistance)
                {
                    result.View = view;
                    result.ViewDistance = hit.distance;
                }
            }
            return result;
        }

        private void LogMissDiagnostics(Vector2 pointer, Ray ray)
        {
            Debug.Log($"Selection viewport: pointer={pointer}, screen={Screen.width}x{Screen.height}, cameraPixels={sceneCamera.pixelWidth}x{sceneCamera.pixelHeight}, pixelRect={sceneCamera.pixelRect}, viewport={sceneCamera.ScreenToViewportPoint(pointer)}, cameraPosition={sceneCamera.transform.position}, cameraRotation={sceneCamera.transform.rotation}", this);
            foreach (PieceData piece in lab.World.Pieces)
            {
                if (!lab.TryGetView(piece.Id, out PieceView view)) continue;
                BoxCollider collider = view.GetComponent<BoxCollider>();
                Vector3 center = collider.bounds.center;
                bool boundsIntersect = collider.bounds.IntersectRay(ray);
                bool centerRayHit = Physics.Raycast(sceneCamera.transform.position, center - sceneCamera.transform.position,
                    out RaycastHit centerHit, 1000f);
                Debug.Log($"Selection block: id={piece.Id}, active={view.gameObject.activeInHierarchy}, layer={view.gameObject.layer}, colliderEnabled={collider.enabled}, colliderCenter={center}, colliderSize={collider.bounds.size}, screenCenter={sceneCamera.WorldToScreenPoint(center)}, pointerRayIntersectsBounds={boundsIntersect}, centerRayHit={(centerRayHit ? centerHit.collider.name : "none")}", view);
            }
            foreach (string name in new[] { "Lab Scale 1m", "Lab Scale 5m", "Lab Scale 10m" })
            {
                GameObject reference = GameObject.Find(name);
                if (reference == null || !reference.TryGetComponent(out Renderer renderer)) continue;
                Debug.Log($"Selection reference: name={name}, screenCenter={sceneCamera.WorldToScreenPoint(renderer.bounds.center)}, pointerRayIntersectsBounds={renderer.bounds.IntersectRay(ray)}, boundsCenter={renderer.bounds.center}, boundsSize={renderer.bounds.size}, collider={reference.GetComponent<Collider>()}", reference);
            }
        }

        private struct PickResult
        {
            public GizmoHandle Handle;
            public PieceView View;
            public float HandleDistance;
            public float ViewDistance;
        }

        private void UpdateManipulation(Vector2 pointer)
        {
            PieceData changed = session.Evaluate(pointer);
            if (snapSettings.HasGeometricSnap && (session.Mode == ManipulationMode.Move ||
                session.Mode == ManipulationMode.Resize && session.ResizeBehavior == ResizeMode.Face))
            {
                changed = geometricSnap.Resolve(changed, session, snapSettings, lab.World.Pieces);
                if (geometricSnap.HasTarget)
                {
                    Vector3 marker = geometricSnap.TargetPoint;
                    Debug.DrawLine(marker - Vector3.up * 0.2f, marker + Vector3.up * 0.2f, Color.cyan);
                    Debug.DrawLine(marker - Vector3.right * 0.2f, marker + Vector3.right * 0.2f, Color.cyan);
                }
            }
            if (!lab.World.TryGet(changed.Id, out PieceData current)) return;
            if (changed.Transform.Equals(current.Transform) && changed.Dimensions.Equals(current.Dimensions)) return;
            lab.Apply(changed);
        }

        public void PointerUp(Vector2 pointer)
        {
            cityCamera.EndPan();
            draggingPan = false;
            if (session != null)
            {
                EndManipulation();
                return;
            }
            bool isClick = IsClick(pressPosition, pointer);
            if (debugSelection) Debug.Log($"Selection PointerUp: start={pressPosition}, end={pointer}, click={isClick}, pressedPiece={pressedPieceId}", this);
            if (!isClick) return;
            PieceId? next = pressedPieceId;
            PieceId? old = selection.SelectedPieceId;
            if (old is PieceId oldId && lab.TryGetView(oldId, out PieceView oldView)) oldView.SetSelected(false);
            if (next is PieceId nextId && lab.World.TryGet(nextId, out _) && lab.TryGetView(nextId, out PieceView nextView))
            {
                selection.Select(nextId);
                nextView.SetSelected(true);
                if (debugSelection) Debug.Log($"Selection applied: {nextId}", this);
            }
            else
            {
                selection.Clear();
                if (debugSelection) Debug.Log("Selection cleared", this);
            }
        }

        private void OnDisable() => EndManipulation();

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) EndManipulation();
        }

        private void EndManipulation()
        {
            session = null;
            geometricSnap.Reset();
            if (cityCamera != null)
            {
                cityCamera.EndPan();
                cityCamera.SetPanSuppressed(false);
            }
        }

        private void OnDestroy()
        {
            if (gizmo != null) Destroy(gizmo.gameObject);
        }
    }
}
