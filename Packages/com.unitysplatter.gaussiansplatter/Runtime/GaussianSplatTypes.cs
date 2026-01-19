using System;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting
{
    [Serializable]
    public struct GaussianSplatPoint
    {
        public Vector3 Position;
        public Vector3 Scale;
        public Quaternion Rotation;
        public Color Color;
        public float Opacity;

        public GaussianSplatPoint(Vector3 position, Vector3 scale, Quaternion rotation, Color color, float opacity)
        {
            Position = position;
            Scale = scale;
            Rotation = rotation;
            Color = color;
            Opacity = opacity;
        }
    }

    [Serializable]
    public sealed class GaussianSplatFrameData
    {
        [SerializeField] private Vector3[] positions = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] scales = Array.Empty<Vector3>();
        [SerializeField] private Quaternion[] rotations = Array.Empty<Quaternion>();
        [SerializeField] private Color[] colors = Array.Empty<Color>();
        [SerializeField] private float[] opacities = Array.Empty<float>();

        public Vector3[] Positions => positions;
        public Vector3[] Scales => scales;
        public Quaternion[] Rotations => rotations;
        public Color[] Colors => colors;
        public float[] Opacities => opacities;
        public int Count => positions.Length;

        public void SetData(Vector3[] newPositions, Vector3[] newScales, Quaternion[] newRotations, Color[] newColors, float[] newOpacities)
        {
            positions = newPositions ?? Array.Empty<Vector3>();
            scales = newScales ?? Array.Empty<Vector3>();
            rotations = newRotations ?? Array.Empty<Quaternion>();
            colors = newColors ?? Array.Empty<Color>();
            opacities = newOpacities ?? Array.Empty<float>();
        }

        public bool IsValid(out string error)
        {
            error = string.Empty;
            if (positions == null || scales == null || rotations == null || colors == null || opacities == null)
            {
                error = "Gaussian splat frame arrays must be initialized.";
                return false;
            }

            int count = positions.Length;
            if (scales.Length != count || rotations.Length != count || colors.Length != count || opacities.Length != count)
            {
                error = "Gaussian splat frame arrays must have matching lengths.";
                return false;
            }

            return true;
        }
    }
}
