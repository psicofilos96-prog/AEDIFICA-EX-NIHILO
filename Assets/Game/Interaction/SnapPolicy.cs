using System;
using UnityEngine;

namespace Aedifica.Interaction
{
    // Pure transform assistance. The model receives the result; this class holds no world state.
    public static class SnapPolicy
    {
        public static void ValidateIncrement(float increment)
        {
            if (!(increment > 0f) || float.IsNaN(increment) || float.IsInfinity(increment))
                throw new ArgumentOutOfRangeException(nameof(increment), "Snap increment must be positive and finite.");
        }

        public static float Quantize(float value, float increment, bool enabled = true)
        {
            if (!enabled) return value;
            ValidateIncrement(increment);
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            double steps = Math.Round((double)value / increment, 0, MidpointRounding.AwayFromZero);
            double result = steps * increment;
            if (double.IsInfinity(result) || result > float.MaxValue || result < -float.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value));
            return (float)result;
        }

        public static Vector3 Quantize(Vector3 value, float increment, bool enabled = true)
        {
            if (!enabled) return value;
            return new Vector3(Quantize(value.x, increment), Quantize(value.y, increment), Quantize(value.z, increment));
        }

        public static float FromSession(float initial, float candidate, float increment, bool enabled = true)
        {
            if (!enabled) return candidate;
            ValidateIncrement(increment);
            if (float.IsNaN(initial) || float.IsInfinity(initial) || float.IsNaN(candidate) || float.IsInfinity(candidate))
                throw new ArgumentOutOfRangeException(nameof(candidate));
            if (candidate == initial) return initial;
            float nearest = Quantize(initial, increment);
            bool aligned = Math.Abs((double)initial - nearest) <= increment * 0.00001;
            double firstGrid = candidate > initial
                ? (aligned ? (double)nearest + increment : Math.Ceiling((double)initial / increment) * increment)
                : (aligned ? (double)nearest - increment : Math.Floor((double)initial / increment) * increment);
            double midpoint = ((double)initial + firstGrid) * 0.5;
            if (candidate > initial ? candidate < midpoint : candidate > midpoint) return initial;
            return Quantize(candidate, increment);
        }
    }
}
