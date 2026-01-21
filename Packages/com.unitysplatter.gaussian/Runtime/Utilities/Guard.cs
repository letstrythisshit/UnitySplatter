using System;

namespace UnitySplatter.Gaussian.Utilities
{
    internal static class Guard
    {
        public static void NotNull(object value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        public static void Positive(int value, string name)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "Value must be positive.");
            }
        }

        public static void Positive(float value, string name)
        {
            if (value <= 0f)
            {
                throw new ArgumentOutOfRangeException(name, value, "Value must be positive.");
            }
        }

        public static void InRange(float value, float min, float max, string name)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(name, value, $"Value must be in range [{min}, {max}].");
            }
        }
    }
}
