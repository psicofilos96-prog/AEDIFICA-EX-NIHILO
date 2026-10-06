using System;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct MaterialId : IEquatable<MaterialId>
    {
        private readonly string value;
        private MaterialId(string value) => this.value = value;
        public bool IsValid => !string.IsNullOrEmpty(value);
        public static bool TryParse(string text, out MaterialId id)
        {
            if (Guid.TryParse(text, out Guid parsed) && parsed != Guid.Empty)
            {
                id = new MaterialId(parsed.ToString("N"));
                return true;
            }
            id = default;
            return false;
        }
        public static MaterialId Parse(string text)
        {
            if (!TryParse(text, out MaterialId id)) throw new ArgumentException("A MaterialId must be a nonempty GUID.", nameof(text));
            return id;
        }
        public override string ToString() => value ?? string.Empty;
        public bool Equals(MaterialId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MaterialId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public static bool operator ==(MaterialId left, MaterialId right) => left.Equals(right);
        public static bool operator !=(MaterialId left, MaterialId right) => !left.Equals(right);
    }

    public static class LabMaterialIds
    {
        public static readonly MaterialId Neutral = MaterialId.Parse("10000000000040008000000000000001");
        public static readonly MaterialId Stone = MaterialId.Parse("10000000000040008000000000000002");
        public static readonly MaterialId Brick = MaterialId.Parse("10000000000040008000000000000003");
        public static readonly MaterialId Plaster = MaterialId.Parse("10000000000040008000000000000004");
    }
}
