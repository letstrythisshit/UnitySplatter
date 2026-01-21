using System.Collections.Generic;
using UnityEngine;
using UnitySplatter.Gaussian.Utilities;

namespace UnitySplatter.Gaussian.IO
{
    public static class GaussianSplatLoader
    {
        public static GaussianSplatAsset LoadFromFile(string path)
        {
            Guard.NotNull(path, nameof(path));
            var splats = PlyParser.Load(path, out var bounds);
            var asset = ScriptableObject.CreateInstance<GaussianSplatAsset>();
            asset.Initialize(splats, bounds, path, false);
            return asset;
        }

        public static GaussianSplatAsset LoadFromSplats(List<GaussianSplatData> splats, Bounds bounds, string path, bool baked)
        {
            Guard.NotNull(splats, nameof(splats));
            var asset = ScriptableObject.CreateInstance<GaussianSplatAsset>();
            asset.Initialize(splats, bounds, path, baked);
            return asset;
        }
    }
}
