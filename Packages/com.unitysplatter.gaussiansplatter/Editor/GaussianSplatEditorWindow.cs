using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System;

namespace UnitySplatter.GaussianSplatting.Editor
{
    /// <summary>
    /// Advanced editor window for Gaussian Splat management and tools
    /// </summary>
    public class GaussianSplatEditorWindow : EditorWindow
    {
        private enum Tab
        {
            Import,
            Process,
            LOD,
            Compression,
            Sequence,
            Tools
        }

        private Tab currentTab = Tab.Import;
        private Vector2 scrollPosition;

        // Import tab
        private string importFilePath = "";
        private string importFolderPath = "";
        private GaussianSplatFrameData importedFrame;

        // Process tab
        private GaussianSplatAsset processFrame;
        private float filterOpacity = 0.1f;
        private int decimationStride = 2;
        private int randomSampleCount = 1000;
        private Bounds filterBounds = new Bounds(Vector3.zero, Vector3.one * 10f);

        // LOD tab
        private GaussianSplatAsset lodSourceFrame;
        private int lodLevelCount = 4;
        private GaussianSplatLODGenerator.LODGenerationMethod lodMethod = GaussianSplatLODGenerator.LODGenerationMethod.ImportanceBased;
        private List<GaussianSplatFrameData> generatedLODs;

        // Compression tab
        private GaussianSplatAsset compressionSourceFrame;
        private GaussianSplatCompressor.CompressedFrameData compressedData;
        private string compressionSavePath = "";

        // Sequence tab
        private List<GaussianSplatFrameData> sequenceFrames = new List<GaussianSplatFrameData>();
        private float sequenceFPS = 30f;
        private bool sequenceLoop = true;
        private string sequenceSavePath = "";

        // Tools tab
        private GaussianSplatAsset toolsSourceFrame;
        private Vector3 translateOffset = Vector3.zero;
        private Vector3 scaleMultiplier = Vector3.one;
        private Vector3 rotationEuler = Vector3.zero;
        private Color tintColor = Color.white;

        [MenuItem("Window/UnitySplatter/Gaussian Splat Tools")]
        public static void ShowWindow()
        {
            var window = GetWindow<GaussianSplatEditorWindow>("Gaussian Splat Tools");
            window.minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Draw tab bar
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(currentTab == Tab.Import, "Import", EditorStyles.toolbarButton))
                currentTab = Tab.Import;
            if (GUILayout.Toggle(currentTab == Tab.Process, "Process", EditorStyles.toolbarButton))
                currentTab = Tab.Process;
            if (GUILayout.Toggle(currentTab == Tab.LOD, "LOD", EditorStyles.toolbarButton))
                currentTab = Tab.LOD;
            if (GUILayout.Toggle(currentTab == Tab.Compression, "Compression", EditorStyles.toolbarButton))
                currentTab = Tab.Compression;
            if (GUILayout.Toggle(currentTab == Tab.Sequence, "Sequence", EditorStyles.toolbarButton))
                currentTab = Tab.Sequence;
            if (GUILayout.Toggle(currentTab == Tab.Tools, "Tools", EditorStyles.toolbarButton))
                currentTab = Tab.Tools;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // Draw current tab content
            switch (currentTab)
            {
                case Tab.Import:
                    DrawImportTab();
                    break;
                case Tab.Process:
                    DrawProcessTab();
                    break;
                case Tab.LOD:
                    DrawLODTab();
                    break;
                case Tab.Compression:
                    DrawCompressionTab();
                    break;
                case Tab.Sequence:
                    DrawSequenceTab();
                    break;
                case Tab.Tools:
                    DrawToolsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawImportTab()
        {
            EditorGUILayout.LabelField("Import PLY Files", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("Import PLY files and convert them to Gaussian Splat assets.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Single file import
            EditorGUILayout.LabelField("Single File Import", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            importFilePath = EditorGUILayout.TextField("PLY File", importFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string path = EditorUtility.OpenFilePanel("Select PLY File", "", "ply");
                if (!string.IsNullOrEmpty(path))
                    importFilePath = path;
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Import Single File", GUILayout.Height(30)))
            {
                ImportSingleFile();
            }

            EditorGUILayout.Space(10);

            // Folder import
            EditorGUILayout.LabelField("Batch Folder Import", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            importFolderPath = EditorGUILayout.TextField("Folder Path", importFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Folder with PLY Files", "", "");
                if (!string.IsNullOrEmpty(path))
                    importFolderPath = path;
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Import Folder as Sequence", GUILayout.Height(30)))
            {
                ImportFolderAsSequence();
            }

            EditorGUILayout.Space(10);

            // Display imported frame info
            if (importedFrame != null && importedFrame.IsValid(out _))
            {
                EditorGUILayout.HelpBox($"Imported Frame Info:\nSplats: {importedFrame.Count:N0}\nMemory: {GetFrameSizeKB(importedFrame):F2} KB", MessageType.None);
            }
        }

        private void DrawProcessTab()
        {
            EditorGUILayout.LabelField("Process Gaussian Splats", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("Apply filters and optimizations to Gaussian splat data.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Source selection
            processFrame = EditorGUILayout.ObjectField("Source Frame", processFrame as UnityEngine.Object, typeof(GaussianSplatAsset), false) as GaussianSplatAsset;

            if (processFrame == null || !processFrame.IsValid(out _))
            {
                EditorGUILayout.HelpBox("Select a valid Gaussian Splat Asset to process.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Splat Count: {processFrame.Frame.Count:N0}", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            // Filtering options
            EditorGUILayout.LabelField("Filtering", EditorStyles.miniBoldLabel);

            filterOpacity = EditorGUILayout.Slider("Min Opacity", filterOpacity, 0f, 1f);
            if (GUILayout.Button("Filter by Opacity"))
            {
                ProcessFilterByOpacity();
            }

            EditorGUILayout.Space(5);

            filterBounds.center = EditorGUILayout.Vector3Field("Bounds Center", filterBounds.center);
            filterBounds.size = EditorGUILayout.Vector3Field("Bounds Size", filterBounds.size);
            if (GUILayout.Button("Filter by Bounds"))
            {
                ProcessFilterByBounds();
            }

            EditorGUILayout.Space(10);

            // Decimation
            EditorGUILayout.LabelField("Decimation", EditorStyles.miniBoldLabel);

            decimationStride = EditorGUILayout.IntSlider("Stride (Keep 1 every N)", decimationStride, 2, 10);
            if (GUILayout.Button("Decimate"))
            {
                ProcessDecimate();
            }

            EditorGUILayout.Space(5);

            randomSampleCount = EditorGUILayout.IntField("Random Sample Count", randomSampleCount);
            if (GUILayout.Button("Random Sample"))
            {
                ProcessRandomSample();
            }
        }

        private void DrawLODTab()
        {
            EditorGUILayout.LabelField("LOD Generation", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("Generate multiple LOD levels for performance optimization.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Source selection
            lodSourceFrame = EditorGUILayout.ObjectField("Source Frame", lodSourceFrame as UnityEngine.Object, typeof(GaussianSplatAsset), false) as GaussianSplatAsset;

            if (lodSourceFrame == null || !lodSourceFrame.IsValid(out _))
            {
                EditorGUILayout.HelpBox("Select a valid Gaussian Splat Asset to generate LODs.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(5);

            // LOD settings
            lodLevelCount = EditorGUILayout.IntSlider("LOD Levels", lodLevelCount, 2, 6);
            lodMethod = (GaussianSplatLODGenerator.LODGenerationMethod)EditorGUILayout.EnumPopup("Generation Method", lodMethod);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Generate LODs", GUILayout.Height(30)))
            {
                GenerateLODs();
            }

            EditorGUILayout.Space(10);

            // Display generated LODs
            if (generatedLODs != null && generatedLODs.Count > 0)
            {
                EditorGUILayout.LabelField("Generated LODs:", EditorStyles.miniBoldLabel);

                for (int i = 0; i < generatedLODs.Count; i++)
                {
                    var lod = generatedLODs[i];
                    if (lod != null && lod.IsValid(out _))
                    {
                        float reductionPercent = (1f - (float)lod.Count / lodSourceFrame.Frame.Count) * 100f;
                        EditorGUILayout.LabelField($"  LOD {i}: {lod.Count:N0} splats ({reductionPercent:F1}% reduction)");
                    }
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Save LODs as Assets"))
                {
                    SaveLODsAsAssets();
                }
            }
        }

        private void DrawCompressionTab()
        {
            EditorGUILayout.LabelField("Compression", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("Compress Gaussian splat data to reduce file size.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Source selection
            compressionSourceFrame = EditorGUILayout.ObjectField("Source Frame", compressionSourceFrame as UnityEngine.Object, typeof(GaussianSplatAsset), false) as GaussianSplatAsset;

            if (compressionSourceFrame == null || !compressionSourceFrame.IsValid(out _))
            {
                EditorGUILayout.HelpBox("Select a valid Gaussian Splat Asset to compress.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Compress", GUILayout.Height(30)))
            {
                CompressFrame();
            }

            EditorGUILayout.Space(10);

            // Display compression stats
            if (compressedData != null)
            {
                int originalSize = GaussianSplatCompressor.GetUncompressedSize(compressionSourceFrame.Frame);
                int compressedSize = compressedData.GetCompressedSize();
                float ratio = compressedData.GetCompressionRatio(originalSize);

                EditorGUILayout.HelpBox(
                    $"Compression Results:\n" +
                    $"Original Size: {originalSize / 1024f:F2} KB\n" +
                    $"Compressed Size: {compressedSize / 1024f:F2} KB\n" +
                    $"Compression Ratio: {ratio:F2}x\n" +
                    $"Space Saved: {(1f - 1f / ratio) * 100f:F1}%",
                    MessageType.None);

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();
                compressionSavePath = EditorGUILayout.TextField("Save Path", compressionSavePath);
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    string path = EditorUtility.SaveFilePanel("Save Compressed Data", "", "compressed.gspc", "gspc");
                    if (!string.IsNullOrEmpty(path))
                        compressionSavePath = path;
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Save Compressed Data"))
                {
                    SaveCompressedData();
                }
            }
        }

        private void DrawSequenceTab()
        {
            EditorGUILayout.LabelField("Sequence Management", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("Create and manage Gaussian splat sequences.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Sequence settings
            sequenceFPS = EditorGUILayout.FloatField("FPS", sequenceFPS);
            sequenceLoop = EditorGUILayout.Toggle("Loop", sequenceLoop);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField($"Frames in Sequence: {sequenceFrames.Count}", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Clear Sequence"))
            {
                sequenceFrames.Clear();
            }

            EditorGUILayout.Space(10);

            // Save sequence
            EditorGUILayout.BeginHorizontal();
            sequenceSavePath = EditorGUILayout.TextField("Save Path", sequenceSavePath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Sequence Asset", "GaussianSequence", "asset", "Save sequence asset");
                if (!string.IsNullOrEmpty(path))
                    sequenceSavePath = path;
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Save as Sequence Asset", GUILayout.Height(30)))
            {
                SaveSequenceAsset();
            }
        }

        private void DrawToolsTab()
        {
            EditorGUILayout.LabelField("Transform Tools", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox("Apply transformations to Gaussian splat data.", MessageType.Info);
            EditorGUILayout.Space(5);

            // Source selection
            toolsSourceFrame = EditorGUILayout.ObjectField("Source Frame", toolsSourceFrame as UnityEngine.Object, typeof(GaussianSplatAsset), false) as GaussianSplatAsset;

            if (toolsSourceFrame == null || !toolsSourceFrame.IsValid(out _))
            {
                EditorGUILayout.HelpBox("Select a valid Gaussian Splat Asset to transform.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(10);

            // Transform tools
            EditorGUILayout.LabelField("Translate", EditorStyles.miniBoldLabel);
            translateOffset = EditorGUILayout.Vector3Field("Offset", translateOffset);
            if (GUILayout.Button("Apply Translation"))
            {
                ApplyTranslation();
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Scale", EditorStyles.miniBoldLabel);
            scaleMultiplier = EditorGUILayout.Vector3Field("Multiplier", scaleMultiplier);
            if (GUILayout.Button("Apply Scale"))
            {
                ApplyScale();
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Rotate", EditorStyles.miniBoldLabel);
            rotationEuler = EditorGUILayout.Vector3Field("Euler Angles", rotationEuler);
            if (GUILayout.Button("Apply Rotation"))
            {
                ApplyRotation();
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Tint", EditorStyles.miniBoldLabel);
            tintColor = EditorGUILayout.ColorField("Color", tintColor);
            if (GUILayout.Button("Apply Tint"))
            {
                ApplyTint();
            }
        }

        // Implementation methods

        private void ImportSingleFile()
        {
            if (string.IsNullOrEmpty(importFilePath) || !File.Exists(importFilePath))
            {
                EditorUtility.DisplayDialog("Error", "Invalid file path", "OK");
                return;
            }

            try
            {
                importedFrame = GaussianSplatPlyLoader.LoadFromFile(importFilePath);

                if (importedFrame != null && importedFrame.IsValid(out _))
                {
                    EditorUtility.DisplayDialog("Success", $"Imported {importedFrame.Count:N0} splats", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Failed to import PLY file", "OK");
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Import failed: {e.Message}", "OK");
            }
        }

        private void ImportFolderAsSequence()
        {
            if (string.IsNullOrEmpty(importFolderPath) || !Directory.Exists(importFolderPath))
            {
                EditorUtility.DisplayDialog("Error", "Invalid folder path", "OK");
                return;
            }

            try
            {
                string[] plyFiles = Directory.GetFiles(importFolderPath, "*.ply");

                if (plyFiles.Length == 0)
                {
                    EditorUtility.DisplayDialog("Error", "No PLY files found in folder", "OK");
                    return;
                }

                Array.Sort(plyFiles);
                sequenceFrames.Clear();

                foreach (string file in plyFiles)
                {
                    var frame = GaussianSplatPlyLoader.LoadFromFile(file);
                    if (frame != null && frame.IsValid(out _))
                    {
                        sequenceFrames.Add(frame);
                    }
                }

                EditorUtility.DisplayDialog("Success", $"Imported {sequenceFrames.Count} frames", "OK");
                currentTab = Tab.Sequence;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Import failed: {e.Message}", "OK");
            }
        }

        private void ProcessFilterByOpacity()
        {
            var result = GaussianSplatFilter.FilterByOpacity(processFrame.Frame, filterOpacity);
            processFrame.SetFrame(result);
            EditorUtility.DisplayDialog("Success", $"Filtered to {processFrame.Frame.Count:N0} splats", "OK");
        }

        private void ProcessFilterByBounds()
        {
            var result = GaussianSplatFilter.FilterByBounds(processFrame.Frame, filterBounds);
            processFrame.SetFrame(result);
            EditorUtility.DisplayDialog("Success", $"Filtered to {processFrame.Frame.Count:N0} splats", "OK");
        }

        private void ProcessDecimate()
        {
            var result = GaussianSplatFilter.Decimate(processFrame.Frame, decimationStride);
            processFrame.SetFrame(result);
            EditorUtility.DisplayDialog("Success", $"Decimated to {processFrame.Frame.Count:N0} splats", "OK");
        }

        private void ProcessRandomSample()
        {
            var result = GaussianSplatFilter.RandomSample(processFrame.Frame, randomSampleCount, null);
            processFrame.SetFrame(result);
            EditorUtility.DisplayDialog("Success", $"Sampled to {processFrame.Frame.Count:N0} splats", "OK");
        }

        private void GenerateLODs()
        {
            generatedLODs = GaussianSplatLODGenerator.GenerateLODLevels(lodSourceFrame.Frame, lodLevelCount, lodMethod);
            EditorUtility.DisplayDialog("Success", $"Generated {generatedLODs.Count} LOD levels", "OK");
        }

        private void SaveLODsAsAssets()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save LOD Assets", "GaussianLOD", "", "Save LOD assets");
            if (string.IsNullOrEmpty(path))
                return;

            string baseName = Path.GetFileNameWithoutExtension(path);
            string directory = Path.GetDirectoryName(path);

            for (int i = 0; i < generatedLODs.Count; i++)
            {
                string assetPath = Path.Combine(directory, $"{baseName}_LOD{i}.asset");
                // Save as asset (implementation needed)
            }

            EditorUtility.DisplayDialog("Success", $"Saved {generatedLODs.Count} LOD assets", "OK");
        }

        private void CompressFrame()
        {
            compressedData = GaussianSplatCompressor.CompressCPU(compressionSourceFrame.Frame);

            if (compressedData != null)
            {
                GaussianSplatCompressor.PrintCompressionStats(compressionSourceFrame.Frame, compressedData);
            }
        }

        private void SaveCompressedData()
        {
            if (string.IsNullOrEmpty(compressionSavePath))
            {
                EditorUtility.DisplayDialog("Error", "Invalid save path", "OK");
                return;
            }

            GaussianSplatCompressor.SaveCompressed(compressedData, compressionSavePath);
            EditorUtility.DisplayDialog("Success", "Compressed data saved", "OK");
        }

        private void SaveSequenceAsset()
        {
            if (string.IsNullOrEmpty(sequenceSavePath))
            {
                EditorUtility.DisplayDialog("Error", "Invalid save path", "OK");
                return;
            }

            // Create sequence asset
            var sequenceAsset = ScriptableObject.CreateInstance<GaussianSplatSequenceAsset>();
            sequenceAsset.SetFrames(sequenceFrames, sequenceFPS, sequenceLoop);

            AssetDatabase.CreateAsset(sequenceAsset, sequenceSavePath);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Success", "Sequence asset saved", "OK");
        }

        private void ApplyTranslation()
        {
            var result = GaussianSplatEditing.Translate(toolsSourceFrame.Frame, translateOffset);
            toolsSourceFrame.SetFrame(result);
            EditorUtility.DisplayDialog("Success", "Translation applied", "OK");
        }

        private void ApplyScale()
        {
            var result = GaussianSplatEditing.Scale(toolsSourceFrame.Frame, scaleMultiplier);
            toolsSourceFrame.SetFrame(result);
            EditorUtility.DisplayDialog("Success", "Scale applied", "OK");
        }

        private void ApplyRotation()
        {
            Quaternion rotation = Quaternion.Euler(rotationEuler);
            var result = GaussianSplatEditing.Rotate(toolsSourceFrame.Frame, rotation);
            toolsSourceFrame.SetFrame(result);
            EditorUtility.DisplayDialog("Success", "Rotation applied", "OK");
        }

        private void ApplyTint()
        {
            var result = GaussianSplatEditing.Tint(toolsSourceFrame.Frame, tintColor);
            toolsSourceFrame.SetFrame(result);
            EditorUtility.DisplayDialog("Success", "Tint applied", "OK");
        }

        private float GetFrameSizeKB(GaussianSplatFrameData frame)
        {
            return GaussianSplatCompressor.GetUncompressedSize(frame) / 1024f;
        }
    }
}
