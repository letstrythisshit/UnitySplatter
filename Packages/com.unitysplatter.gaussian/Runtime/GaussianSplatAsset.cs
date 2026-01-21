using System;
using UnityEngine;

namespace UnitySplatter.Gaussian.Runtime
{
    [CreateAssetMenu(fileName = "GaussianSplatAsset", menuName = "UnitySplatter/Gaussian Splat Asset", order = 1)]
    public sealed class GaussianSplatAsset : ScriptableObject
    {
        [SerializeField] private GaussianPoint[] points = Array.Empty<GaussianPoint>();
        [SerializeField] private Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
        [SerializeField] private string sourcePath = string.Empty;
        [SerializeField] private string sourceHash = string.Empty;
        [SerializeField] private int version = 1;

        public ReadOnlySpan<GaussianPoint> Points => points;
        public Bounds Bounds => bounds;
        public string SourcePath => sourcePath;
        public string SourceHash => sourceHash;
        public int Version => version;

        public int Count => points?.Length ?? 0;

        public void SetData(GaussianPoint[] newPoints, Bounds newBounds, string path, string hash)
        {
            points = newPoints ?? Array.Empty<GaussianPoint>();
            bounds = newBounds;
            sourcePath = path ?? string.Empty;
            sourceHash = hash ?? string.Empty;
        }
    }
}
