using UnityEngine;
using UnityEditor;

namespace UnitySplatter.GaussianSplatting.Editor
{
    [CustomEditor(typeof(GaussianSplatStreamingPlayer))]
    public class GaussianSplatStreamingPlayerEditor : UnityEditor.Editor
    {
        private SerializedProperty streamingAssetsPath;
        private SerializedProperty useAbsolutePath;
        private SerializedProperty fps;
        private SerializedProperty loop;
        private SerializedProperty playOnEnable;
        private SerializedProperty preloadNextFrame;
        private SerializedProperty maxCachedFrames;
        private SerializedProperty preloadFrameCount;
        private SerializedProperty useCompression;
        private SerializedProperty useBackgroundLoading;
        private SerializedProperty loadThreadPriority;

        private void OnEnable()
        {
            streamingAssetsPath = serializedObject.FindProperty("streamingAssetsPath");
            useAbsolutePath = serializedObject.FindProperty("useAbsolutePath");
            fps = serializedObject.FindProperty("fps");
            loop = serializedObject.FindProperty("loop");
            playOnEnable = serializedObject.FindProperty("playOnEnable");
            preloadNextFrame = serializedObject.FindProperty("preloadNextFrame");
            maxCachedFrames = serializedObject.FindProperty("maxCachedFrames");
            preloadFrameCount = serializedObject.FindProperty("preloadFrameCount");
            useCompression = serializedObject.FindProperty("useCompression");
            useBackgroundLoading = serializedObject.FindProperty("useBackgroundLoading");
            loadThreadPriority = serializedObject.FindProperty("loadThreadPriority");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var player = (GaussianSplatStreamingPlayer)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Gaussian Splat Streaming Player", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Streams large Gaussian splat sequences from disk with frame prediction and caching.", MessageType.Info);
            EditorGUILayout.Space(10);

            // Source Settings
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(streamingAssetsPath, new GUIContent("Sequence Path"));
            EditorGUILayout.PropertyField(useAbsolutePath, new GUIContent("Use Absolute Path"));

            if (!useAbsolutePath.boolValue)
            {
                EditorGUILayout.HelpBox($"Path will be relative to StreamingAssets folder:\n{System.IO.Path.Combine(Application.streamingAssetsPath, streamingAssetsPath.stringValue)}", MessageType.None);
            }

            EditorGUILayout.Space(10);

            // Playback Settings
            EditorGUILayout.LabelField("Playback", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(fps);
            EditorGUILayout.PropertyField(loop);
            EditorGUILayout.PropertyField(playOnEnable, new GUIContent("Play on Enable"));
            EditorGUILayout.PropertyField(preloadNextFrame, new GUIContent("Enable Preloading"));

            EditorGUILayout.Space(10);

            // Memory Management
            EditorGUILayout.LabelField("Memory Management", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(maxCachedFrames, new GUIContent("Max Cached Frames"));
            EditorGUILayout.PropertyField(preloadFrameCount, new GUIContent("Preload Frame Count", "Number of frames to preload ahead"));
            EditorGUILayout.PropertyField(useCompression, new GUIContent("Use Compression"));

            EditorGUILayout.Space(10);

            // Performance Settings
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useBackgroundLoading, new GUIContent("Background Loading", "Load frames asynchronously"));
            EditorGUILayout.PropertyField(loadThreadPriority, new GUIContent("Thread Priority"));

            EditorGUILayout.Space(10);

            // Runtime Controls and Statistics
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Playback Control", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (player.IsPlaying)
                {
                    if (GUILayout.Button("Pause"))
                    {
                        player.Pause();
                    }
                    if (GUILayout.Button("Stop"))
                    {
                        player.Stop();
                    }
                }
                else
                {
                    if (GUILayout.Button("Play"))
                    {
                        player.Play();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Seek bar
                int currentFrame = player.CurrentFrame;
                int totalFrames = player.TotalFrames;

                if (totalFrames > 0)
                {
                    EditorGUILayout.LabelField($"Frame: {currentFrame} / {totalFrames}");

                    int newFrame = EditorGUILayout.IntSlider("", currentFrame, 0, totalFrames - 1);
                    if (newFrame != currentFrame)
                    {
                        player.Seek(newFrame);
                    }

                    // Progress bar
                    Rect progressRect = GUILayoutUtility.GetRect(18, 18, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(progressRect, player.Progress, $"{player.Progress * 100f:F1}%");
                }

                EditorGUILayout.Space(10);

                // Statistics
                EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Total Frames", player.TotalFrames);
                EditorGUILayout.IntField("Cached Frames", player.GetCachedFrameCount());
                EditorGUILayout.FloatField("Cache Hit Rate", player.GetCacheHitRate() * 100f);
                EditorGUILayout.FloatField("Avg Load Time (ms)", player.GetAverageLoadTime() * 1000f);
                EditorGUILayout.Toggle("Is Playing", player.IsPlaying);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(5);

                // Cache health indicator
                float cacheHealth = player.GetCacheHitRate();
                MessageType healthType = cacheHealth > 0.9f ? MessageType.Info :
                                       cacheHealth > 0.7f ? MessageType.Warning :
                                       MessageType.Error;

                EditorGUILayout.HelpBox($"Cache Health: {cacheHealth * 100f:F1}%\n" +
                    (cacheHealth > 0.9f ? "Excellent - Smooth playback" :
                     cacheHealth > 0.7f ? "Good - Consider increasing cache size" :
                     "Poor - Increase cache size or reduce FPS"), healthType);

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Force Refresh"))
                {
                    Repaint();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see playback controls and statistics.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();

            // Auto-repaint in play mode
            if (Application.isPlaying && player.IsPlaying)
            {
                Repaint();
            }
        }
    }
}
