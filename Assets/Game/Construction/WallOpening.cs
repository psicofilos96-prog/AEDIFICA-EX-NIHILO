using System;

namespace Aedifica.Construction
{
    public enum WallOpeningKind { Passage, Window }

    // X is measured from the wall's left end; Bottom is measured from its base.
    // Both coordinates stay local when the host wall is moved or rotated.
    [Serializable]
    public readonly struct WallOpening : IEquatable<WallOpening>
    {
        public const float MinimumSize = 0.2f;
        public const float MinimumSolid = 0.1f;

        public Guid Id { get; }
        public PieceId HostWallId { get; }
        public WallOpeningKind Kind { get; }
        public float Left { get; }
        public float Bottom { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => Left + Width;
        public float Top => Bottom + Height;

        public WallOpening(Guid id, PieceId hostWallId, WallOpeningKind kind,
            float left, float bottom, float width, float height)
        {
            if (id == Guid.Empty) throw new ArgumentException("Opening ID must be nonempty.", nameof(id));
            if (!hostWallId.IsValid) throw new ArgumentException("Host wall ID must be valid.", nameof(hostWallId));
            if (kind != WallOpeningKind.Passage && kind != WallOpeningKind.Window)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Finite(left) || !Finite(bottom) || !Finite(width) || !Finite(height) ||
                left < 0f || bottom < 0f || width < MinimumSize || height < MinimumSize ||
                !Finite(left + width) || !Finite(bottom + height))
                throw new ArgumentOutOfRangeException(nameof(width), "Opening coordinates and sizes must be finite and positive.");
            Id = id;
            HostWallId = hostWallId;
            Kind = kind;
            Left = left;
            Bottom = bottom;
            Width = width;
            Height = height;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public WallOpening WithRect(float left, float bottom, float width, float height) =>
            new WallOpening(Id, HostWallId, Kind, left, bottom, width, height);
        public bool Equals(WallOpening other) => Id == other.Id && HostWallId == other.HostWallId &&
            Kind == other.Kind && Left == other.Left && Bottom == other.Bottom &&
            Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is WallOpening other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
