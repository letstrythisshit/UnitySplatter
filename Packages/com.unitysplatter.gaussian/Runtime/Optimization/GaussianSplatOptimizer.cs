using System;
using System.Collections.Generic;
using UnityEngine;
using UnitySplatter.Gaussian.Utilities;

namespace UnitySplatter.Gaussian.Optimization
{
    public static class GaussianSplatOptimizer
    {
        public static List<GaussianSplatData> RemoveDuplicates(IReadOnlyList<GaussianSplatData> input, float positionEpsilon)
        {
            Guard.NotNull(input, nameof(input));
            Guard.Positive(positionEpsilon, nameof(positionEpsilon));

            var result = new List<GaussianSplatData>(input.Count);
            var grid = new Dictionary<Vector3Int, GaussianSplatData>();
            var inv = 1f / positionEpsilon;

            foreach (var splat in input)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(splat.Position.x * inv),
                    Mathf.RoundToInt(splat.Position.y * inv),
                    Mathf.RoundToInt(splat.Position.z * inv));

                if (!grid.ContainsKey(key))
                {
                    grid[key] = splat;
                    result.Add(splat);
                }
            }

            return result;
        }

        public static void NormalizeOpacity(List<GaussianSplatData> splats, float targetMax = 1f)
        {
            Guard.NotNull(splats, nameof(splats));
            Guard.Positive(targetMax, nameof(targetMax));

            var maxOpacity = 0f;
            foreach (var splat in splats)
            {
                maxOpacity = Mathf.Max(maxOpacity, splat.Opacity);
            }

            if (maxOpacity <= 0f)
            {
                return;
            }

            var scale = targetMax / maxOpacity;
            for (var i = 0; i < splats.Count; i++)
            {
                var splat = splats[i];
                splat.Opacity = Mathf.Clamp01(splat.Opacity * scale);
                splats[i] = splat;
            }
        }

        public static Bounds ComputeBounds(IReadOnlyList<GaussianSplatData> splats)
        {
            Guard.NotNull(splats, nameof(splats));
            if (splats.Count == 0)
            {
                return new Bounds();
            }

            var bounds = new Bounds(splats[0].Position, Vector3.zero);
            for (var i = 1; i < splats.Count; i++)
            {
                bounds.Encapsulate(splats[i].Position);
            }

            return bounds;
        }
    }
}
