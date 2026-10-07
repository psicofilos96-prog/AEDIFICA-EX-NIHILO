using System;
using Aedifica.Construction;
using UnityEngine;

namespace Aedifica.Interaction
{
    public sealed class ManipulationSession
    {
        public const float MinimumDimension = 0.1f;
        private const float DegreesPerPixel = 0.5f;

        public PieceData InitialPiece { get; }
        public ManipulationMode Mode { get; }
        public ManipulationAxis Axis { get; }
        public ResizeMode ResizeBehavior { get; }
        public int FaceSign { get; }

        private readonly Vector2 startPointer;
        private readonly Vector2 screenAxis;
        private readonly float pixelsPerMeter;
        private readonly bool positionSnapEnabled;
        private readonly bool rotationSnapEnabled;
        private readonly float positionIncrement;
        private readonly float rotationIncrementDegrees;
        private readonly float initialYaw;

        public ManipulationSession(PieceData initialPiece, ManipulationMode mode, ManipulationAxis axis,
            Vector2 startPointer, Vector2 screenAxis, float pixelsPerMeter, SnapSettings snapSettings = null,
            ResizeMode resizeMode = ResizeMode.Center, int faceSign = 1)
        {
            InitialPiece = initialPiece ?? throw new ArgumentNullException(nameof(initialPiece));
            if (mode != ManipulationMode.Move && mode != ManipulationMode.Rotate && mode != ManipulationMode.Resize) throw new ArgumentOutOfRangeException(nameof(mode));
            if (axis != ManipulationAxis.X && axis != ManipulationAxis.Y && axis != ManipulationAxis.Z) throw new ArgumentOutOfRangeException(nameof(axis));
            if (mode == ManipulationMode.Rotate && axis != ManipulationAxis.Y) throw new ArgumentException("Rotate supports Y only.", nameof(axis));
            if (mode != ManipulationMode.Rotate && (!IsFinite(pixelsPerMeter) || pixelsPerMeter <= 0f))
                throw new ArgumentOutOfRangeException(nameof(pixelsPerMeter));
            if (resizeMode != ResizeMode.Center && resizeMode != ResizeMode.Face) throw new ArgumentOutOfRangeException(nameof(resizeMode));
            if (faceSign != 1 && faceSign != -1) throw new ArgumentOutOfRangeException(nameof(faceSign));
            Mode = mode;
            Axis = axis;
            ResizeBehavior = resizeMode;
            FaceSign = faceSign;
            this.startPointer = startPointer;
            this.screenAxis = screenAxis.sqrMagnitude > 0f ? screenAxis.normalized : Vector2.right;
            this.pixelsPerMeter = pixelsPerMeter;
            snapSettings?.ValidateEnabled();
            positionSnapEnabled = snapSettings != null && snapSettings.PositionSnapEnabled;
            rotationSnapEnabled = snapSettings != null && snapSettings.RotationSnapEnabled;
            positionIncrement = positionSnapEnabled ? snapSettings.PositionIncrement : 0f;
            rotationIncrementDegrees = rotationSnapEnabled ? snapSettings.RotationIncrementDegrees : 0f;
            initialYaw = initialPiece.Transform.Rotation.eulerAngles.y;
        }

        public PieceData Evaluate(Vector2 pointer)
        {
            Vector2 drag = pointer - startPointer;
            if (!IsFinite(drag.x) || !IsFinite(drag.y)) return InitialPiece;
            if (Mode == ManipulationMode.Rotate)
            {
                float degrees = drag.x * DegreesPerPixel;
                if (!IsFinite(degrees)) return InitialPiece;
                float rotationDelta = rotationSnapEnabled
                    ? SnapPolicy.FromSession(initialYaw, initialYaw + degrees, rotationIncrementDegrees) - initialYaw
                    : degrees;
                Quaternion rotation = Quaternion.AngleAxis(rotationDelta, Vector3.up) * InitialPiece.Transform.Rotation;
                return InitialPiece.WithTransform(new PieceTransform(InitialPiece.Transform.Position, rotation));
            }

            float meters = Vector2.Dot(drag, screenAxis) / pixelsPerMeter;
            if (!IsFinite(meters)) return InitialPiece;
            if (Mode == ManipulationMode.Move)
            {
                Vector3 start = InitialPiece.Transform.Position;
                Vector3 position = start + AxisVector(Axis) * meters;
                position[(int)Axis] = SnapPolicy.FromSession(start[(int)Axis], position[(int)Axis],
                    positionIncrement, positionSnapEnabled);
                return InitialPiece.WithTransform(new PieceTransform(position,
                    InitialPiece.Transform.Rotation));
            }

            PieceDimensions old = InitialPiece.Dimensions;
            float current = Axis == ManipulationAxis.X ? old.X : Axis == ManipulationAxis.Y ? old.Y : old.Z;
            float dimension = Mathf.Max(MinimumDimension, current + meters);
            PieceData resized = InitialPiece.WithDimensions(old.Resize((int)Axis, dimension));
            if (ResizeBehavior == ResizeMode.Center) return resized;

            float actualChange = dimension - current;
            Vector3 localBaseShift = AxisVector(Axis) * (FaceSign * actualChange * 0.5f);
            // PieceTransform.Position is the bottom-center of the mesh, not its geometric center.
            if (Axis == ManipulationAxis.Y) localBaseShift -= Vector3.up * (actualChange * 0.5f);
            Vector3 position = InitialPiece.Transform.Position + InitialPiece.Transform.Rotation * localBaseShift;
            return resized.WithTransform(new PieceTransform(position, InitialPiece.Transform.Rotation));
        }

        public static Vector3 AxisVector(ManipulationAxis axis)
        {
            switch (axis)
            {
                case ManipulationAxis.X: return Vector3.right;
                case ManipulationAxis.Y: return Vector3.up;
                case ManipulationAxis.Z: return Vector3.forward;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
