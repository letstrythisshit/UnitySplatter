using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnitySplatter.Gaussian.IO;
using UnitySplatter.Gaussian.Optimization;

namespace UnitySplatter.Gaussian.Editor
{
    [ScriptedImporter(1, "ply")]
    public sealed class PlySplatImporter : ScriptedImporter
    {
        [SerializeField] private bool normalizeOpacity = true;
        [SerializeField, Range(0.0001f, 0.05f)] private float duplicatePositionEpsilon = 0.001f;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var splats = PlyParser.Load(ctx.assetPath, out var bounds);

            if (duplicatePositionEpsilon > 0f)
            {
                splats = GaussianSplatOptimizer.RemoveDuplicates(splats, duplicatePositionEpsilon);
                bounds = GaussianSplatOptimizer.ComputeBounds(splats);
            }

            if (normalizeOpacity)
            {
                GaussianSplatOptimizer.NormalizeOpacity(splats);
            }

            var asset = ScriptableObject.CreateInstance<GaussianSplatAsset>();
            asset.Initialize(splats, bounds, ctx.assetPath, false);

            ctx.AddObjectToAsset("GaussianSplatAsset", asset);
            ctx.SetMainObject(asset);
        }
    }
}
