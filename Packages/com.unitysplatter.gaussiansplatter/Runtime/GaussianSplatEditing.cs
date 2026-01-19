using System;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting
{
    public static class GaussianSplatEditing
    {
        public static GaussianSplatFrameData Translate(GaussianSplatFrameData source, Vector3 offset)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Vector3[] positions = new Vector3[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                positions[i] = source.Positions[i] + offset;
            }

            GaussianSplatFrameData frame = new();
            frame.SetData(positions, source.Scales, source.Rotations, source.Colors, source.Opacities);
            return frame;
        }

        public static GaussianSplatFrameData Scale(GaussianSplatFrameData source, Vector3 scaleMultiplier)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Vector3[] positions = new Vector3[source.Count];
            Vector3[] scales = new Vector3[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                positions[i] = Vector3.Scale(source.Positions[i], scaleMultiplier);
                scales[i] = Vector3.Scale(source.Scales[i], scaleMultiplier);
            }

            GaussianSplatFrameData frame = new();
            frame.SetData(positions, scales, source.Rotations, source.Colors, source.Opacities);
            return frame;
        }

        public static GaussianSplatFrameData Rotate(GaussianSplatFrameData source, Quaternion rotation)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Vector3[] positions = new Vector3[source.Count];
            Quaternion[] rotations = new Quaternion[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                positions[i] = rotation * source.Positions[i];
                rotations[i] = rotation * source.Rotations[i];
            }

            GaussianSplatFrameData frame = new();
            frame.SetData(positions, source.Scales, rotations, source.Colors, source.Opacities);
            return frame;
        }

        public static GaussianSplatFrameData Tint(GaussianSplatFrameData source, Color tint)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Color[] colors = new Color[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                colors[i] = source.Colors[i] * tint;
            }

            GaussianSplatFrameData frame = new();
            frame.SetData(source.Positions, source.Scales, source.Rotations, colors, source.Opacities);
            return frame;
        }
    }
}
