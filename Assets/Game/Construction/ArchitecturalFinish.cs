using System;

namespace Aedifica.Construction
{
    public enum ArchitecturalFinishKind { DoorFrame, WindowFrameSill, Lintel, RoofEave }

    public static class ArchitecturalFinishMaterials
    {
        // Same shared material ID already used by the visual reference registry.
        public static readonly MaterialId Glass = MaterialId.Parse("20000000000040008000000000000004");
    }

    // Identity and host-local parameters only. No cached world transform or GameObject reference.
    [Serializable]
    public readonly struct ArchitecturalFinish : IEquatable<ArchitecturalFinish>
    {
        public Guid Id { get; }
        public PieceId HostId { get; }
        public Guid OpeningId { get; }
        public ArchitecturalFinishKind Kind { get; }
        public MaterialId MaterialId { get; }
        public float Profile { get; }
        public float Projection { get; }

        public ArchitecturalFinish(Guid id, PieceId hostId, Guid openingId,
            ArchitecturalFinishKind kind, MaterialId materialId, float profile = 0.14f,
            float projection = 0.08f)
        {
            if (id == Guid.Empty || !hostId.IsValid || !materialId.IsValid)
                throw new ArgumentException("A finish needs stable identity, host and material IDs.");
            if (!Enum.IsDefined(typeof(ArchitecturalFinishKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Finite(profile) || !Finite(projection) || profile < 0.05f ||
                profile > 0.3f || projection < 0.01f || projection > 0.3f)
                throw new ArgumentOutOfRangeException(nameof(profile));
            if (kind == ArchitecturalFinishKind.RoofEave ? openingId != Guid.Empty : openingId == Guid.Empty)
                throw new ArgumentException("Only opening finishes need an opening ID.", nameof(openingId));
            Id=id;HostId=hostId;OpeningId=openingId;Kind=kind;
            MaterialId=materialId;Profile=profile;Projection=projection;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public bool Equals(ArchitecturalFinish other) => Id==other.Id && HostId==other.HostId &&
            OpeningId==other.OpeningId && Kind==other.Kind && MaterialId==other.MaterialId &&
            Profile==other.Profile && Projection==other.Projection;
        public override bool Equals(object obj) => obj is ArchitecturalFinish other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
    }
}
