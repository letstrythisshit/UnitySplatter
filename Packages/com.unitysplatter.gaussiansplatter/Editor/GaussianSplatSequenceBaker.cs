using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting.Editor
{
    public static class GaussianSplatSequenceBaker
    {
        [MenuItem("UnitySplatter/Bake Gaussian Splat Sequence")]
        public static void BakeSequence()
        {
            string folder = EditorUtility.OpenFolderPanel("Select PLY Sequence Folder", "", "");
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            string assetPath = EditorUtility.SaveFilePanelInProject("Save Baked Gaussian Splat Sequence", "GaussianSplatSequence", "asset", "Choose a location to save the baked sequence asset.");
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            string[] plyFiles = Directory.GetFiles(folder, "*.ply").OrderBy(path => path).ToArray();
            if (plyFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Gaussian Splat Sequence", "No PLY files found in the selected folder.", "OK");
                return;
            }

            List<GaussianSplatFrameData> frames = new();
            try
            {
                for (int i = 0; i < plyFiles.Length; i++)
                {
                    string plyFile = plyFiles[i];
                    EditorUtility.DisplayProgressBar("Baking Gaussian Splats", $"Processing {Path.GetFileName(plyFile)}", i / (float)plyFiles.Length);
                    GaussianSplatFrameData frame = GaussianSplatPlyLoader.LoadFromFile(plyFile);
                    frames.Add(frame);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            GaussianSplatSequenceAsset sequence = ScriptableObject.CreateInstance<GaussianSplatSequenceAsset>();
            sequence.SetFrames(frames, fps: 30f, shouldLoop: true);

            AssetDatabase.CreateAsset(sequence, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Gaussian Splat Sequence", $"Baked {frames.Count} frames to {assetPath}.", "OK");
        }
    }
}
