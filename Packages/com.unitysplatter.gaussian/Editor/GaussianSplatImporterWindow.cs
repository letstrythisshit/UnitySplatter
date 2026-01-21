using System.IO;
using UnityEditor;
using UnityEngine;
using UnitySplatter.Gaussian.Runtime;

namespace UnitySplatter.Gaussian.Editor
{
    public sealed class GaussianSplatImporterWindow : EditorWindow
    {
        private string sourcePath = string.Empty;
        private string outputPath = "Assets/GaussianSplatAsset.asset";
        private float opacityThreshold = 0.01f;
        private float decimateRatio = 1f;
        private int decimateSeed = 1234;
        private Vector3 minScale = new Vector3(0.001f, 0.001f, 0.001f);
        private Vector3 maxScale = new Vector3(5f, 5f, 5f);

        [MenuItem("UnitySplatter/Gaussian Splat Importer")]
        public static void Open()
        {
            GetWindow<GaussianSplatImporterWindow>("Gaussian Splat Importer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source PLY", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            sourcePath = EditorGUILayout.TextField(sourcePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string selected = EditorUtility.OpenFilePanel("Select PLY", string.Empty, "ply");
                if (!string.IsNullOrEmpty(selected))
                {
                    sourcePath = selected;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Output Asset", EditorStyles.boldLabel);
            outputPath = EditorGUILayout.TextField(outputPath);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optimization", EditorStyles.boldLabel);
            opacityThreshold = EditorGUILayout.Slider("Min Opacity", opacityThreshold, 0f, 1f);
            decimateRatio = EditorGUILayout.Slider("Decimate Ratio", decimateRatio, 0.01f, 1f);
            decimateSeed = EditorGUILayout.IntField("Decimate Seed", decimateSeed);
            minScale = EditorGUILayout.Vector3Field("Min Scale", minScale);
            maxScale = EditorGUILayout.Vector3Field("Max Scale", maxScale);

            EditorGUILayout.Space();
            if (GUILayout.Button("Import"))
            {
                Import();
            }
        }

        private void Import()
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                EditorUtility.DisplayDialog("Import Failed", "Select a valid PLY file.", "OK");
                return;
            }

            GaussianPoint[] points = PlyReader.Load(sourcePath, out Bounds bounds);
            points = GaussianSplatOptimizer.FilterByOpacity(points, opacityThreshold);
            points = GaussianSplatOptimizer.RandomDecimate(points, decimateRatio, decimateSeed);
            points = GaussianSplatOptimizer.ClampScale(points, minScale, maxScale);

            var asset = CreateInstance<GaussianSplatAsset>();
            asset.SetData(points, bounds, sourcePath, string.Empty);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(outputPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Import Complete", $"Created asset at {assetPath}", "OK");
        }
    }
}
