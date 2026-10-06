using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct PieceId : IEquatable<PieceId>, IComparable<PieceId>
    {
        private readonly string value;

        private PieceId(string value) => this.value = value;

        public bool IsValid => !string.IsNullOrEmpty(value);

        public static bool TryParse(string text, out PieceId id)
        {
            if (Guid.TryParse(text, out Guid parsed) && parsed != Guid.Empty)
            {
                id = new PieceId(parsed.ToString("N"));
                return true;
            }
            id = default;
            return false;
        }

        public static PieceId Parse(string text)
        {
            if (!TryParse(text, out PieceId id)) throw new ArgumentException("A PieceId must be a nonempty GUID.", nameof(text));
            return id;
        }

        public override string ToString() => value ?? string.Empty;
        public bool Equals(PieceId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PieceId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public int CompareTo(PieceId other) => string.Compare(value, other.value, StringComparison.Ordinal);
        public static bool operator ==(PieceId left, PieceId right) => left.Equals(right);
        public static bool operator !=(PieceId left, PieceId right) => !left.Equals(right);
    }

    public static class PieceIdGenerator
    {
        public static PieceId New() => PieceId.Parse(Guid.NewGuid().ToString("N"));
    }
}
