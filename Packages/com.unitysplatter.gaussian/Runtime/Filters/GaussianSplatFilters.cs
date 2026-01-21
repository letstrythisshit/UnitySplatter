using System.Collections.Generic;
using UnityEngine;
using UnitySplatter.Gaussian.Utilities;

namespace UnitySplatter.Gaussian.Filters
{
    public static class GaussianSplatFilters
    {
        public static List<GaussianSplatData> FilterByOpacity(IReadOnlyList<GaussianSplatData> input, float minOpacity)
        {
            Guard.NotNull(input, nameof(input));
            Guard.InRange(minOpacity, 0f, 1f, nameof(minOpacity));
            var result = new List<GaussianSplatData>(input.Count);
            foreach (var splat in input)
            {
                if (splat.Opacity >= minOpacity)
                {
                    result.Add(splat);
                }
            }
            return result;
        }

        public static List<GaussianSplatData> FilterByBounds(IReadOnlyList<GaussianSplatData> input, Bounds bounds)
        {
            Guard.NotNull(input, nameof(input));
            var result = new List<GaussianSplatData>(input.Count);
            foreach (var splat in input)
            {
                if (bounds.Contains(splat.Position))
                {
                    result.Add(splat);
                }
            }
            return result;
        }

        public static List<GaussianSplatData> Decimate(IReadOnlyList<GaussianSplatData> input, int stride)
        {
            Guard.NotNull(input, nameof(input));
            Guard.Positive(stride, nameof(stride));
            var result = new List<GaussianSplatData>((input.Count + stride - 1) / stride);
            for (var i = 0; i < input.Count; i += stride)
            {
                result.Add(input[i]);
            }
            return result;
        }
    }
}
