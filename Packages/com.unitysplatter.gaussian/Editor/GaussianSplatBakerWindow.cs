using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnitySplatter.Gaussian.IO;
using UnitySplatter.Gaussian.Playback;

namespace UnitySplatter.Gaussian.Editor
{
    public sealed class GaussianSplatBakerWindow : EditorWindow
    {
        private DefaultAsset sourceDirectory;
        private float frameRate = 30f;
        private string outputPath = "Assets/GaussianSplatSequence.asset";

        [MenuItem("UnitySplatter/Gaussian Splat Baker")]
        public static void Open()
        {
            GetWindow<GaussianSplatBakerWindow>("Gaussian Splat Baker");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Gaussian Splat Sequence Baker", EditorStyles.boldLabel);
            sourceDirectory = (DefaultAsset)EditorGUILayout.ObjectField("Source Directory", sourceDirectory, typeof(DefaultAsset), false);
            frameRate = EditorGUILayout.FloatField("Frame Rate", frameRate);
            outputPath = EditorGUILayout.TextField("Output Asset Path", outputPath);

            if (GUILayout.Button("Bake Sequence"))
            {
                Bake();
            }
        }

        private void Bake()
        {
            if (sourceDirectory == null)
            {
                EditorUtility.DisplayDialog("Gaussian Splat Baker", "Select a source directory containing PLY files.", "OK");
                return;
            }

            var directoryPath = AssetDatabase.GetAssetPath(sourceDirectory);
            if (!AssetDatabase.IsValidFolder(directoryPath))
            {
                EditorUtility.DisplayDialog("Gaussian Splat Baker", "Invalid directory selection.", "OK");
                return;
            }

            var absolutePath = Path.GetFullPath(directoryPath);
            var files = Directory.GetFiles(absolutePath, "*.ply");
            System.Array.Sort(files);

            var frames = new List<GaussianSplatAsset>();
            foreach (var file in files)
            {
                try
                {
                    var asset = GaussianSplatLoader.LoadFromFile(file);
                    frames.Add(asset);
                }
                catch (IOException ex)
                {
                    Debug.LogError($"Failed to load {file}: {ex.Message}");
                }
            }

            var sequence = ScriptableObject.CreateInstance<GaussianSplatSequenceAsset>();
            sequence.Initialize(frames, frameRate, directoryPath);

            var assetPath = outputPath;
            if (!assetPath.EndsWith(".asset"))
            {
                assetPath += ".asset";
            }

            AssetDatabase.CreateAsset(sequence, assetPath);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Gaussian Splat Baker", "Sequence baked successfully.", "OK");
        }
    }
}
