using System;
using UnityEngine;

namespace UnitySplatter.Gaussian
{
    [Serializable]
    public struct GaussianSplatData
    {
        public Vector3 Position;
        public Vector3 Scale;
        public Quaternion Rotation;
        public Color Color;
        public float Opacity;

        public GaussianSplatData(Vector3 position, Vector3 scale, Quaternion rotation, Color color, float opacity)
        {
            Position = position;
            Scale = scale;
            Rotation = rotation;
            Color = color;
            Opacity = opacity;
        }
    }
}
