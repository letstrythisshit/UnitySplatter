using System;
using UnityEngine;

namespace UnitySplatter.Gaussian.Runtime
{
    [Serializable]
    public struct GaussianPoint
    {
        public Vector3 Position;
        public Vector3 Scale;
        public Quaternion Rotation;
        public Color Color;
        public float Opacity;
        public float SH0;
        public float SH1;
        public float SH2;
        public float SH3;
        public float SH4;
        public float SH5;
        public float SH6;
        public float SH7;
        public float SH8;
    }
}
