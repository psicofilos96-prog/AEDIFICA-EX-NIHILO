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

        private readonly Vector2 startPointer;
        private readonly Vector2 screenAxis;
        private readonly float pixelsPerMeter;

        public ManipulationSession(PieceData initialPiece, ManipulationMode mode, ManipulationAxis axis,
            Vector2 startPointer, Vector2 screenAxis, float pixelsPerMeter)
        {
            InitialPiece = initialPiece ?? throw new ArgumentNullException(nameof(initialPiece));
            if (mode != ManipulationMode.Move && mode != ManipulationMode.Rotate && mode != ManipulationMode.Resize) throw new ArgumentOutOfRangeException(nameof(mode));
            if (axis != ManipulationAxis.X && axis != ManipulationAxis.Y && axis != ManipulationAxis.Z) throw new ArgumentOutOfRangeException(nameof(axis));
            if (mode == ManipulationMode.Rotate && axis != ManipulationAxis.Y) throw new ArgumentException("Rotate supports Y only.", nameof(axis));
            if (mode != ManipulationMode.Rotate && (!IsFinite(pixelsPerMeter) || pixelsPerMeter <= 0f))
                throw new ArgumentOutOfRangeException(nameof(pixelsPerMeter));
            Mode = mode;
            Axis = axis;
            this.startPointer = startPointer;
            this.screenAxis = screenAxis.sqrMagnitude > 0f ? screenAxis.normalized : Vector2.right;
            this.pixelsPerMeter = pixelsPerMeter;
        }

        public PieceData Evaluate(Vector2 pointer)
        {
            Vector2 drag = pointer - startPointer;
            if (!IsFinite(drag.x) || !IsFinite(drag.y)) return InitialPiece;
            if (Mode == ManipulationMode.Rotate)
            {
                float degrees = drag.x * DegreesPerPixel;
                if (!IsFinite(degrees)) return InitialPiece;
                Quaternion rotation = Quaternion.AngleAxis(degrees, Vector3.up) * InitialPiece.Transform.Rotation;
                return InitialPiece.WithTransform(new PieceTransform(InitialPiece.Transform.Position, rotation));
            }

            float meters = Vector2.Dot(drag, screenAxis) / pixelsPerMeter;
            if (!IsFinite(meters)) return InitialPiece;
            if (Mode == ManipulationMode.Move)
            {
                Vector3 axis = AxisVector(Axis);
                return InitialPiece.WithTransform(new PieceTransform(InitialPiece.Transform.Position + axis * meters,
                    InitialPiece.Transform.Rotation));
            }

            BlockDimensions old = InitialPiece.BlockDimensions;
            float width = Axis == ManipulationAxis.X ? Mathf.Max(MinimumDimension, old.Width + meters) : old.Width;
            float height = Axis == ManipulationAxis.Y ? Mathf.Max(MinimumDimension, old.Height + meters) : old.Height;
            float depth = Axis == ManipulationAxis.Z ? Mathf.Max(MinimumDimension, old.Depth + meters) : old.Depth;
            return InitialPiece.WithBlockDimensions(new BlockDimensions(width, height, depth));
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
