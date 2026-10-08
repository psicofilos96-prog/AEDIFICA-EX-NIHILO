using System;
using Aedifica.Construction;
using Aedifica.Interaction.Camera;
using Aedifica.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aedifica.Interaction
{
    public enum OpeningCommand
    {
        None, AddPassage, AddWindow, Next, Remove,
        MoveLeft, MoveRight, MoveUp, MoveDown,
        Narrow, Widen, Shorten, Heighten
    }

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
        private Guid? pressedOpeningId;
        private Guid? selectedOpeningId;
        public Guid? SelectedOpeningId => selectedOpeningId;
        public string LastOpeningFeedback => openingFeedback;
        private PieceId? hudWallId;
        private Guid? hudOpeningId;
        private WallOpening hudOpeningData;
        private bool hudHasOpening;
        private int hudOpeningCount = -1;
        private string hudText;
        private string openingFeedback;
        private float openingFeedbackUntil;
        private Vector2 pressPosition;
        private bool draggingPan;
        private bool lastObservedLeftPressed;
        private OpeningCommand repeatingOpeningCommand;
        private float nextOpeningRepeatTime;
        private const float OpeningRepeatDelay = 0.3f;
        private const float OpeningRepeatInterval = 0.08f;

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
                if (keyboard.leftBracketKey.wasPressedThisFrame) AdjustSelectedStepCount(-1);
                if (keyboard.rightBracketKey.wasPressedThisFrame) AdjustSelectedStepCount(1);
                if (keyboard.homeKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame) FrameSelected();
                ApplyOpeningCommand(ReadOpeningCommandWithRepeat(keyboard));
            }
            else repeatingOpeningCommand = OpeningCommand.None;

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
            pressedOpeningId = null;
            PickResult picked = Pick(pointer);
            GizmoHandle nearestHandle = picked.Handle;
            PieceView nearestPiece = picked.View;
            float handleDistance = picked.HandleDistance;
            float pieceDistance = picked.ViewDistance;
            bool openingUnderPointer = TryPickSelectedOpeningAt(pointer, out Guid openingId, out float openingDistance);
            if (nearestHandle != null && (nearestPiece == null || handleDistance <= pieceDistance) &&
                (!openingUnderPointer || handleDistance <= openingDistance) &&
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
            else if (openingUnderPointer && openingDistance < pieceDistance) pressedOpeningId = openingId;
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

        private void AdjustSelectedStepCount(int delta)
        {
            if (!(selection.SelectedPieceId is PieceId id) || !lab.World.TryGet(id, out PieceData piece) ||
                !piece.Dimensions.IsStair) return;
            int steps = Mathf.Clamp(piece.Dimensions.StepCount + delta, 1, StairDimensions.MaximumStepCount);
            if (steps == piece.Dimensions.StepCount) return;
            if (lab.Apply(piece.WithStepCount(steps))) Debug.Log($"Stair StepCount: {steps}", this);
        }

        public bool FrameSelected()
        {
            if (!(selection.SelectedPieceId is PieceId id) || !lab.TryGetView(id, out PieceView view)) return false;
            Bounds bounds = view.GetComponent<MeshRenderer>().bounds;
            return cityCamera.FrameBounds(bounds);
        }

        // P0.12.1 keyboard editing is scoped to a selected wall and never consumes
        // the existing camera, gizmo, material, or snap commands.
        public static OpeningCommand ReadOpeningCommand(Keyboard keyboard)
        {
            if (keyboard == null) return OpeningCommand.None;
            if (keyboard.insertKey.wasPressedThisFrame)
                return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                    ? OpeningCommand.AddWindow : OpeningCommand.AddPassage;
            if (keyboard.tabKey.wasPressedThisFrame) return OpeningCommand.Next;
            if (keyboard.deleteKey.wasPressedThisFrame) return OpeningCommand.Remove;
            if (keyboard.jKey.wasPressedThisFrame) return OpeningCommand.MoveLeft;
            if (keyboard.lKey.wasPressedThisFrame) return OpeningCommand.MoveRight;
            if (keyboard.iKey.wasPressedThisFrame) return OpeningCommand.MoveUp;
            if (keyboard.kKey.wasPressedThisFrame) return OpeningCommand.MoveDown;
            if (keyboard.uKey.wasPressedThisFrame) return OpeningCommand.Narrow;
            if (keyboard.oKey.wasPressedThisFrame) return OpeningCommand.Widen;
            if (keyboard.nKey.wasPressedThisFrame) return OpeningCommand.Shorten;
            if (keyboard.bKey.wasPressedThisFrame) return OpeningCommand.Heighten;
            return OpeningCommand.None;
        }

        private OpeningCommand ReadOpeningCommandWithRepeat(Keyboard keyboard)
        {
            OpeningCommand pressed = ReadOpeningCommand(keyboard);
            if (pressed != OpeningCommand.None)
            {
                repeatingOpeningCommand = IsAdjustment(pressed) ? pressed : OpeningCommand.None;
                nextOpeningRepeatTime = Time.unscaledTime + OpeningRepeatDelay;
                return pressed;
            }
            if (repeatingOpeningCommand == OpeningCommand.None ||
                !AdjustmentKeyHeld(keyboard, repeatingOpeningCommand))
            {
                repeatingOpeningCommand = OpeningCommand.None;
                return OpeningCommand.None;
            }
            if (Time.unscaledTime < nextOpeningRepeatTime) return OpeningCommand.None;
            nextOpeningRepeatTime = Time.unscaledTime + OpeningRepeatInterval;
            return repeatingOpeningCommand;
        }

        private static bool IsAdjustment(OpeningCommand command) =>
            command >= OpeningCommand.MoveLeft && command <= OpeningCommand.Heighten;

        private static bool AdjustmentKeyHeld(Keyboard keyboard, OpeningCommand command)
        {
            switch (command)
            {
                case OpeningCommand.MoveLeft: return keyboard.jKey.isPressed;
                case OpeningCommand.MoveRight: return keyboard.lKey.isPressed;
                case OpeningCommand.MoveUp: return keyboard.iKey.isPressed;
                case OpeningCommand.MoveDown: return keyboard.kKey.isPressed;
                case OpeningCommand.Narrow: return keyboard.uKey.isPressed;
                case OpeningCommand.Widen: return keyboard.oKey.isPressed;
                case OpeningCommand.Shorten: return keyboard.nKey.isPressed;
                case OpeningCommand.Heighten: return keyboard.bKey.isPressed;
                default: return false;
            }
        }

        private void ApplyOpeningCommand(OpeningCommand command)
        {
            switch (command)
            {
                case OpeningCommand.AddPassage: AddOpening(WallOpeningKind.Passage); break;
                case OpeningCommand.AddWindow: AddOpening(WallOpeningKind.Window); break;
                case OpeningCommand.Next: CycleOpening(); break;
                case OpeningCommand.Remove: RemoveSelectedOpening(); break;
                case OpeningCommand.MoveLeft: MoveSelectedOpening(-0.1f, 0f); break;
                case OpeningCommand.MoveRight: MoveSelectedOpening(0.1f, 0f); break;
                case OpeningCommand.MoveUp: MoveSelectedOpening(0f, 0.1f); break;
                case OpeningCommand.MoveDown: MoveSelectedOpening(0f, -0.1f); break;
                case OpeningCommand.Narrow: ResizeSelectedOpening(-0.1f, 0f); break;
                case OpeningCommand.Widen: ResizeSelectedOpening(0.1f, 0f); break;
                case OpeningCommand.Shorten: ResizeSelectedOpening(0f, -0.1f); break;
                case OpeningCommand.Heighten: ResizeSelectedOpening(0f, 0.1f); break;
            }
        }

        public bool AddOpening(WallOpeningKind kind)
        {
            if (!RequireSelectedWall(out PieceData wall)) return false;
            float width = 1.2f;
            float height = kind == WallOpeningKind.Passage ? 2f : 1f;
            float bottom = kind == WallOpeningKind.Passage ? 0f : 1f;
            Guid id = Guid.NewGuid();
            float centeredLeft = (wall.Dimensions.X - width) * 0.5f;
            int slots = Mathf.Max(0, Mathf.CeilToInt((wall.Dimensions.X - width - WallOpening.MinimumSolid - 0.2f) * 10f));
            for (int slot = -1; slot <= slots; slot++)
            {
                float left = slot < 0 ? centeredLeft : 0.2f + slot * 0.1f;
                PieceData changed;
                try
                {
                    changed = wall.WithOpening(new WallOpening(id, wall.Id, kind, left, bottom, width, height));
                }
                catch (ArgumentException) { continue; }
                if (!lab.Apply(changed)) return false;
                selectedOpeningId = id;
                Debug.Log($"Opening added: {kind} ({id})", this);
                ShowOpeningFeedback($"{(kind == WallOpeningKind.Window ? "Janela" : "Passagem")} criada e selecionada.");
                return true;
            }
            ShowOpeningFeedback("Sem espaço para este vão: são necessários 0,1 m de parede nas bordas e entre aberturas.", true);
            return false;
        }

        public bool CycleOpening()
        {
            if (!RequireSelectedWall(out PieceData wall)) return false;
            if (wall.Openings.Count == 0)
            {
                ShowOpeningFeedback("Esta parede não tem aberturas. Use Insert para criar uma.");
                return false;
            }
            int index = -1;
            for (int i = 0; i < wall.Openings.Count; i++)
                if (wall.Openings[i].Id == selectedOpeningId) { index = i; break; }
            selectedOpeningId = wall.Openings[(index + 1) % wall.Openings.Count].Id;
            Debug.Log($"Opening selected: {selectedOpeningId}", this);
            ShowOpeningFeedback($"Abertura {((index + 1) % wall.Openings.Count) + 1}/{wall.Openings.Count} selecionada.");
            return true;
        }

        public bool MoveSelectedOpening(float horizontal, float vertical) =>
            EditSelectedOpening(horizontal, vertical, 0f, 0f);

        public bool ResizeSelectedOpening(float width, float height) =>
            EditSelectedOpening(0f, 0f, width, height);

        private bool EditSelectedOpening(float horizontal, float vertical, float width, float height)
        {
            if (!RequireSelectedWall(out PieceData wall) || !RequireActiveOpening(wall, out WallOpening opening)) return false;
            PieceData changed;
            try
            {
                WallOpening edited = opening.WithRect(opening.Left + horizontal, opening.Bottom + vertical,
                    opening.Width + width, opening.Height + height);
                changed = wall.ReplaceOpening(edited);
            }
            catch (ArgumentException)
            {
                string reason = opening.Bottom + vertical < 0f ? "o vão já está no piso" :
                    opening.Width + width < WallOpening.MinimumSize || opening.Height + height < WallOpening.MinimumSize
                        ? "largura e altura mínimas são 0,2 m" :
                    "mantenha 0,1 m de parede nas bordas e entre aberturas";
                ShowOpeningFeedback($"Edição recusada: {reason}.", true);
                return false;
            }
            if (!lab.Apply(changed)) return false;
            ShowOpeningFeedback("Abertura atualizada.");
            return true;
        }

        public bool RemoveSelectedOpening()
        {
            if (!RequireSelectedWall(out PieceData wall) || !RequireActiveOpening(wall, out WallOpening opening)) return false;
            if (!lab.Apply(wall.WithoutOpening(opening.Id))) return false;
            selectedOpeningId = null;
            Debug.Log($"Opening removed: {opening.Id}", this);
            ShowOpeningFeedback("Abertura removida; parede restaurada nesta região.");
            return true;
        }

        private bool RequireSelectedWall(out PieceData wall)
        {
            if (TryGetSelectedWall(out wall)) return true;
            if (selection.SelectedPieceId is PieceId id && lab.World.TryGet(id, out PieceData selected))
                ShowOpeningFeedback($"Aberturas paramétricas funcionam apenas em Wall; peça atual: {selected.Type}.", true);
            else ShowOpeningFeedback("Selecione uma Wall antes de editar aberturas.", true);
            return false;
        }

        private bool RequireActiveOpening(PieceData wall, out WallOpening opening)
        {
            if (TryGetActiveOpening(wall, out opening)) return true;
            ShowOpeningFeedback(wall.Openings.Count == 0
                ? "Esta parede não tem aberturas. Use Insert para criar uma."
                : "Nenhuma abertura ativa. Use Tab para selecionar uma.");
            return false;
        }

        private void ShowOpeningFeedback(string message, bool warning = false)
        {
            bool shouldLog = warning && (openingFeedback != message || Time.unscaledTime > openingFeedbackUntil);
            openingFeedback = message;
            openingFeedbackUntil = Time.unscaledTime + 4f;
            if (shouldLog) Debug.LogWarning(message, this);
        }

        private bool TryGetSelectedWall(out PieceData wall)
        {
            wall = null;
            return selection.SelectedPieceId is PieceId id && lab.World.TryGet(id, out wall) && wall.Type == PieceType.Wall;
        }

        private bool TryGetActiveOpening(PieceData wall, out WallOpening opening)
        {
            foreach (WallOpening candidate in wall.Openings)
                if (candidate.Id == selectedOpeningId)
                {
                    opening = candidate;
                    return true;
                }
            opening = default;
            return false;
        }

        private bool TryPickSelectedOpeningAt(Vector2 pointer, out Guid openingId, out float openingDistance)
        {
            openingId = default;
            openingDistance = float.MaxValue;
            if (!TryGetSelectedWall(out PieceData wall) || wall.Openings.Count == 0) return false;
            Vector3 normal = wall.Transform.Rotation * Vector3.forward;
            var plane = new Plane(normal, wall.Transform.Position);
            Ray ray = sceneCamera.ScreenPointToRay(pointer);
            if (!plane.Raycast(ray, out float distance) ||
                distance <= 0f || distance > 1000f) return false;
            Vector3 worldPoint = ray.GetPoint(distance);
            Vector3 local = Quaternion.Inverse(wall.Transform.Rotation) * (worldPoint - wall.Transform.Position);
            float x = local.x + wall.Dimensions.X * 0.5f;
            foreach (WallOpening opening in wall.Openings)
                if (x > opening.Left && x < opening.Right && local.y > opening.Bottom && local.y < opening.Top)
                {
                    openingId = opening.Id;
                    openingDistance = distance;
                    return true;
                }
            return false;
        }

        private void OnGUI()
        {
            if (openingFeedback != null && Time.unscaledTime <= openingFeedbackUntil)
                GUI.Label(new Rect(12f, 58f, 800f, 24f), openingFeedback);
            if (!TryGetSelectedWall(out PieceData wall)) return;
            bool hasActive = TryGetActiveOpening(wall, out WallOpening active);
            if (hudWallId != wall.Id || hudOpeningId != selectedOpeningId || hudOpeningCount != wall.Openings.Count ||
                hudHasOpening != hasActive || hasActive && !hudOpeningData.Equals(active))
            {
                hudWallId = wall.Id;
                hudOpeningId = selectedOpeningId;
                hudOpeningCount = wall.Openings.Count;
                hudHasOpening = hasActive;
                hudOpeningData = active;
                string current = hasActive
                    ? $"{active.Kind} x={active.Left:F1} y={active.Bottom:F1} w={active.Width:F1} h={active.Height:F1} m"
                    : "nenhuma";
                hudText = $"Wall: {wall.Openings.Count} aberturas | ativa: {current} | Insert passagem, Shift+Insert janela, Tab próxima, Delete remover\n" +
                    "J/L horizontal, I/K vertical, U/O largura, N/B altura (0,1 m por toque)";
            }
            GUI.Label(new Rect(12f, 12f, 800f, 45f), hudText);
            if (hasActive) DrawActiveOpeningOutline(wall, active);
        }

        private void DrawActiveOpeningOutline(PieceData wall, WallOpening opening)
        {
            float nearSide = Vector3.Dot(sceneCamera.transform.position - wall.Transform.Position,
                wall.Transform.Rotation * Vector3.forward) >= 0f ? 1f : -1f;
            float z = nearSide * wall.Dimensions.Z * 0.5f;
            float left = opening.Left - wall.Dimensions.X * 0.5f;
            float right = opening.Right - wall.Dimensions.X * 0.5f;
            Vector3 a = sceneCamera.WorldToScreenPoint(wall.Transform.Position +
                wall.Transform.Rotation * new Vector3(left, opening.Bottom, z));
            Vector3 b = sceneCamera.WorldToScreenPoint(wall.Transform.Position +
                wall.Transform.Rotation * new Vector3(right, opening.Bottom, z));
            Vector3 c = sceneCamera.WorldToScreenPoint(wall.Transform.Position +
                wall.Transform.Rotation * new Vector3(right, opening.Top, z));
            Vector3 d = sceneCamera.WorldToScreenPoint(wall.Transform.Position +
                wall.Transform.Rotation * new Vector3(left, opening.Top, z));
            if (a.z <= 0f || b.z <= 0f || c.z <= 0f || d.z <= 0f) return;
            float x0 = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
            float x1 = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float y0 = Screen.height - Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
            float y1 = Screen.height - Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
            if (x1 <= x0 || y1 <= y0) return;
            Color previous = GUI.color;
            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(x0, y0, x1 - x0, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x0, y1 - 2f, x1 - x0, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x0, y0, 2f, y1 - y0), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x1 - 2f, y0, 2f, y1 - y0), Texture2D.whiteTexture);
            GUI.color = previous;
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
            if (pressedOpeningId is Guid openingId && pressedPieceId == null)
            {
                selectedOpeningId = openingId;
                ShowOpeningFeedback("Abertura selecionada. Use J/L, I/K, U/O e N/B para editar.");
                return;
            }
            PieceId? next = pressedPieceId;
            PieceId? old = selection.SelectedPieceId;
            if (old != next) selectedOpeningId = null;
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

        private void OnDisable()
        {
            repeatingOpeningCommand = OpeningCommand.None;
            EndManipulation();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                repeatingOpeningCommand = OpeningCommand.None;
                EndManipulation();
            }
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
