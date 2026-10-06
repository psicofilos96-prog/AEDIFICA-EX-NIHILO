using System;
using UnityEngine;

namespace Aedifica.Construction
{
    [Serializable]
    public readonly struct PieceTransform : IEquatable<PieceTransform>
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public PieceTransform(Vector3 position, Quaternion rotation)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z))
                throw new ArgumentException("Position must be finite.", nameof(position));
            if (!IsFinite(rotation.x) || !IsFinite(rotation.y) || !IsFinite(rotation.z) || !IsFinite(rotation.w))
                throw new ArgumentException("Rotation must be finite.", nameof(rotation));
            float magnitudeSquared = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            if (!IsFinite(magnitudeSquared) || magnitudeSquared < 0.000001f)
                throw new ArgumentException("Rotation must have nonzero finite magnitude.", nameof(rotation));

            Position = position;
            float inverseMagnitude = 1f / Mathf.Sqrt(magnitudeSquared);
            Rotation = new Quaternion(rotation.x * inverseMagnitude, rotation.y * inverseMagnitude,
                rotation.z * inverseMagnitude, rotation.w * inverseMagnitude);
        }

        public bool IsValid => IsFinite(Position.x) && IsFinite(Position.y) && IsFinite(Position.z)
            && IsFinite(Rotation.x) && IsFinite(Rotation.y) && IsFinite(Rotation.z) && IsFinite(Rotation.w)
            && Rotation.x * Rotation.x + Rotation.y * Rotation.y + Rotation.z * Rotation.z + Rotation.w * Rotation.w > 0.999f;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public bool Equals(PieceTransform other) => Position.Equals(other.Position) && Rotation.Equals(other.Rotation);
        public override bool Equals(object obj) => obj is PieceTransform other && Equals(other);
        public override int GetHashCode() => (Position.GetHashCode() * 397) ^ Rotation.GetHashCode();
    }
}
