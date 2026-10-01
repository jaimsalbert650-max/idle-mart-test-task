using System;

namespace IdleMart.Progression
{
    /// <summary>
    /// Store reputation from 1 to 5 stars. Served customers raise it a little, complaints
    /// (sold out, long queue) lower it more. The rating scales how many customers come.
    /// </summary>
    public sealed class StoreRating
    {
        public const float Min = 1f;
        public const float Max = 5f;
        public const float Default = 3.5f;
        public const float SatisfiedStep = 0.04f;
        public const float ComplaintStep = 0.12f;

        public event Action<float> Changed;

        public float Value { get; private set; } = Default;

        /// <summary>Customer flow multiplier: x0.6 at 1 star, x1.4 at 5 stars.</summary>
        public float FlowMultiplier => 0.6f + 0.8f * (Value - Min) / (Max - Min);

        public void Satisfied() => Add(SatisfiedStep);

        public void Complained() => Add(-ComplaintStep);

        /// <summary>Restores from a save; invalid values fall back to the default.</summary>
        public void Set(float value)
        {
            Value = value >= Min && value <= Max ? value : Default;
            Changed?.Invoke(Value);
        }

        private void Add(float delta)
        {
            var next = Math.Clamp(Value + delta, Min, Max);
            if (Math.Abs(next - Value) < 1e-6f) return;
            Value = next;
            Changed?.Invoke(Value);
        }
    }
}
