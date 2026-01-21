using System;
using UnityEngine;

namespace UnitySplatter.Gaussian.Runtime
{
    public static class GaussianSplatOptimizer
    {
        public static GaussianPoint[] FilterByOpacity(ReadOnlySpan<GaussianPoint> points, float minOpacity)
        {
            if (points.Length == 0)
            {
                return Array.Empty<GaussianPoint>();
            }

            var result = new GaussianPoint[points.Length];
            int count = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].Opacity >= minOpacity)
                {
                    result[count++] = points[i];
                }
            }

            Array.Resize(ref result, count);
            return result;
        }

        public static GaussianPoint[] RandomDecimate(ReadOnlySpan<GaussianPoint> points, float ratio, int seed)
        {
            if (points.Length == 0)
            {
                return Array.Empty<GaussianPoint>();
            }

            ratio = Mathf.Clamp01(ratio);
            int target = Mathf.Max(1, Mathf.RoundToInt(points.Length * ratio));
            var result = new GaussianPoint[target];
            var rng = new System.Random(seed);
            for (int i = 0; i < target; i++)
            {
                int index = rng.Next(points.Length);
                result[i] = points[index];
            }

            return result;
        }

        public static GaussianPoint[] ClampScale(ReadOnlySpan<GaussianPoint> points, Vector3 minScale, Vector3 maxScale)
        {
            var result = new GaussianPoint[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                point.Scale = new Vector3(
                    Mathf.Clamp(point.Scale.x, minScale.x, maxScale.x),
                    Mathf.Clamp(point.Scale.y, minScale.y, maxScale.y),
                    Mathf.Clamp(point.Scale.z, minScale.z, maxScale.z));
                result[i] = point;
            }

            return result;
        }
    }
}
