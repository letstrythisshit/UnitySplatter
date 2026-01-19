using System;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting
{
    public static class GaussianSplatOptimizer
    {
        public static Bounds CalculateBounds(Vector3[] positions, Vector3[] scales)
        {
            if (positions == null || positions.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Vector3 min = positions[0];
            Vector3 max = positions[0];

            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 extent = scales != null && i < scales.Length ? scales[i] : Vector3.zero;
                Vector3 pos = positions[i];
                min = Vector3.Min(min, pos - extent);
                max = Vector3.Max(max, pos + extent);
            }

            Bounds bounds = new();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        public static GaussianSplatFrameData NormalizeScale(GaussianSplatFrameData source, float targetScale)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            targetScale = Mathf.Max(0.0001f, targetScale);

            Vector3[] scales = new Vector3[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                float currentMagnitude = source.Scales[i].magnitude;
                if (currentMagnitude <= 0f)
                {
                    scales[i] = Vector3.one * targetScale;
                }
                else
                {
                    scales[i] = source.Scales[i] * (targetScale / currentMagnitude);
                }
            }

            GaussianSplatFrameData frame = new();
            frame.SetData(source.Positions, scales, source.Rotations, source.Colors, source.Opacities);
            return frame;
        }
    }
}
