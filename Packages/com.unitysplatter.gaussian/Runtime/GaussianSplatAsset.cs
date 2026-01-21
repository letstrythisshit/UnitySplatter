using System.Collections.Generic;
using UnityEngine;

namespace UnitySplatter.Gaussian
{
    [CreateAssetMenu(menuName = "UnitySplatter/Gaussian Splat Asset", fileName = "GaussianSplatAsset")]
    public class GaussianSplatAsset : ScriptableObject
    {
        [SerializeField] private List<GaussianSplatData> splats = new();
        [SerializeField] private Bounds bounds;
        [SerializeField] private string sourcePath;
        [SerializeField] private bool isBaked;

        public IReadOnlyList<GaussianSplatData> Splats => splats;
        public Bounds Bounds => bounds;
        public string SourcePath => sourcePath;
        public bool IsBaked => isBaked;

        public void Initialize(List<GaussianSplatData> data, Bounds dataBounds, string originPath, bool baked)
        {
            splats = data ?? new List<GaussianSplatData>();
            bounds = dataBounds;
            sourcePath = originPath;
            isBaked = baked;
        }
    }
}
