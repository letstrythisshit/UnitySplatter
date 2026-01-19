using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting
{
    public static class GaussianSplatFilter
    {
        public static GaussianSplatFrameData FilterByBounds(GaussianSplatFrameData source, Bounds bounds)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            List<Vector3> positions = new();
            List<Vector3> scales = new();
            List<Quaternion> rotations = new();
            List<Color> colors = new();
            List<float> opacities = new();

            for (int i = 0; i < source.Count; i++)
            {
                if (!bounds.Contains(source.Positions[i]))
                {
                    continue;
                }

                positions.Add(source.Positions[i]);
                scales.Add(source.Scales[i]);
                rotations.Add(source.Rotations[i]);
                colors.Add(source.Colors[i]);
                opacities.Add(source.Opacities[i]);
            }

            GaussianSplatFrameData frame = new();
            frame.SetData(positions.ToArray(), scales.ToArray(), rotations.ToArray(), colors.ToArray(), opacities.ToArray());
            return frame;
        }

        public static GaussianSplatFrameData FilterByOpacity(GaussianSplatFrameData source, float minOpacity)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            minOpacity = Mathf.Clamp01(minOpacity);

            List<Vector3> positions = new();
            List<Vector3> scales = new();
            List<Quaternion> rotations = new();
            List<Color> colors = new();
            List<float> opacities = new();

            for (int i = 0; i < source.Count; i++)
            {
                if (source.Opacities[i] < minOpacity)
                {
                    continue;
                }

                positions.Add(source.Positions[i]);
                scales.Add(source.Scales[i]);
                rotations.Add(source.Rotations[i]);
                colors.Add(source.Colors[i]);
                opacities.Add(source.Opacities[i]);
            }

            GaussianSplatFrameData frame = new();
            frame.SetData(positions.ToArray(), scales.ToArray(), rotations.ToArray(), colors.ToArray(), opacities.ToArray());
            return frame;
        }

        public static GaussianSplatFrameData Decimate(GaussianSplatFrameData source, int stride)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (stride <= 1)
            {
                return source;
            }

            List<Vector3> positions = new();
            List<Vector3> scales = new();
            List<Quaternion> rotations = new();
            List<Color> colors = new();
            List<float> opacities = new();

            for (int i = 0; i < source.Count; i += stride)
            {
                positions.Add(source.Positions[i]);
                scales.Add(source.Scales[i]);
                rotations.Add(source.Rotations[i]);
                colors.Add(source.Colors[i]);
                opacities.Add(source.Opacities[i]);
            }

            GaussianSplatFrameData frame = new();
            frame.SetData(positions.ToArray(), scales.ToArray(), rotations.ToArray(), colors.ToArray(), opacities.ToArray());
            return frame;
        }

        public static GaussianSplatFrameData RandomSample(GaussianSplatFrameData source, int targetCount, int? seed = null)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (targetCount <= 0 || targetCount >= source.Count)
            {
                return source;
            }

            System.Random random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            HashSet<int> indices = new();
            while (indices.Count < targetCount)
            {
                indices.Add(random.Next(0, source.Count));
            }

            List<Vector3> positions = new();
            List<Vector3> scales = new();
            List<Quaternion> rotations = new();
            List<Color> colors = new();
            List<float> opacities = new();

            foreach (int index in indices)
            {
                positions.Add(source.Positions[index]);
                scales.Add(source.Scales[index]);
                rotations.Add(source.Rotations[index]);
                colors.Add(source.Colors[index]);
                opacities.Add(source.Opacities[index]);
            }

            GaussianSplatFrameData frame = new();
            frame.SetData(positions.ToArray(), scales.ToArray(), rotations.ToArray(), colors.ToArray(), opacities.ToArray());
            return frame;
        }
    }
}
