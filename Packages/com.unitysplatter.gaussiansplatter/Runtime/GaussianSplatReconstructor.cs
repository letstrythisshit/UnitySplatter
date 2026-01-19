using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting
{
    public static class GaussianSplatReconstructor
    {
        public static GaussianSplatFrameData MergeFrames(IReadOnlyList<GaussianSplatFrameData> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                throw new ArgumentException("At least one frame is required to merge.", nameof(frames));
            }

            List<Vector3> positions = new();
            List<Vector3> scales = new();
            List<Quaternion> rotations = new();
            List<Color> colors = new();
            List<float> opacities = new();

            foreach (GaussianSplatFrameData frame in frames)
            {
                if (frame == null)
                {
                    continue;
                }

                for (int i = 0; i < frame.Count; i++)
                {
                    positions.Add(frame.Positions[i]);
                    scales.Add(frame.Scales[i]);
                    rotations.Add(frame.Rotations[i]);
                    colors.Add(frame.Colors[i]);
                    opacities.Add(frame.Opacities[i]);
                }
            }

            GaussianSplatFrameData merged = new();
            merged.SetData(positions.ToArray(), scales.ToArray(), rotations.ToArray(), colors.ToArray(), opacities.ToArray());
            return merged;
        }

        public static GaussianSplatFrameData SmoothPositions(GaussianSplatFrameData source, float smoothing)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            smoothing = Mathf.Clamp01(smoothing);
            Vector3[] positions = new Vector3[source.Count];

            for (int i = 0; i < source.Count; i++)
            {
                Vector3 position = source.Positions[i];
                positions[i] = Vector3.Lerp(position, Vector3.zero, smoothing);
            }

            GaussianSplatFrameData smoothed = new();
            smoothed.SetData(positions, source.Scales, source.Rotations, source.Colors, source.Opacities);
            return smoothed;
        }
    }
}
