using System.IO;
using UnityEditor;
using UnityEngine;
using UnitySplatter.Gaussian.Runtime;

namespace UnitySplatter.Gaussian.Editor
{
    public sealed class GaussianSequenceBakerWindow : EditorWindow
    {
        private string sourceDirectory = string.Empty;
        private string outputDirectory = "Assets/GaussianSequence";
        private string sequenceAssetName = "GaussianSequence.asset";
        private float framesPerSecond = 30f;
        private bool loop = true;

        [MenuItem("UnitySplatter/Gaussian Sequence Baker")]
        public static void Open()
        {
            GetWindow<GaussianSequenceBakerWindow>("Gaussian Sequence Baker");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Source PLY Directory", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            sourceDirectory = EditorGUILayout.TextField(sourceDirectory);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Directory", string.Empty, string.Empty);
                if (!string.IsNullOrEmpty(selected))
                {
                    sourceDirectory = selected;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            outputDirectory = EditorGUILayout.TextField("Output Directory", outputDirectory);
            sequenceAssetName = EditorGUILayout.TextField("Sequence Asset Name", sequenceAssetName);
            framesPerSecond = EditorGUILayout.FloatField("Frames Per Second", framesPerSecond);
            loop = EditorGUILayout.Toggle("Loop", loop);

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake Sequence"))
            {
                Bake();
            }
        }

        private void Bake()
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                EditorUtility.DisplayDialog("Bake Failed", "Select a valid source directory.", "OK");
                return;
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string[] files = Directory.GetFiles(sourceDirectory, "*.ply");
            System.Array.Sort(files);
            if (files.Length == 0)
            {
                EditorUtility.DisplayDialog("Bake Failed", "No PLY files found.", "OK");
                return;
            }

            var frames = new GaussianSplatAsset[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                GaussianPoint[] points = PlyReader.Load(file, out Bounds bounds);
                var asset = CreateInstance<GaussianSplatAsset>();
                asset.SetData(points, bounds, file, string.Empty);

                string assetPath = Path.Combine(outputDirectory, $"frame_{i:D04}.asset");
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                AssetDatabase.CreateAsset(asset, assetPath);
                frames[i] = asset;
            }

            var sequenceAsset = CreateInstance<GaussianSplatSequenceAsset>();
            var serializedObject = new SerializedObject(sequenceAsset);
            serializedObject.FindProperty("frames").arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                serializedObject.FindProperty("frames").GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
            serializedObject.FindProperty("framesPerSecond").floatValue = framesPerSecond;
            serializedObject.FindProperty("loop").boolValue = loop;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            string sequencePath = Path.Combine(outputDirectory, sequenceAssetName);
            sequencePath = AssetDatabase.GenerateUniqueAssetPath(sequencePath);
            AssetDatabase.CreateAsset(sequenceAsset, sequencePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Bake Complete", $"Sequence created at {sequencePath}", "OK");
        }
    }
}
