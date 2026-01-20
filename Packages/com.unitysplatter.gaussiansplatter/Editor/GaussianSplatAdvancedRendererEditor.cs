using UnityEngine;
using UnityEditor;

namespace UnitySplatter.GaussianSplatting.Editor
{
    [CustomEditor(typeof(GaussianSplatAdvancedRenderer))]
    public class GaussianSplatAdvancedRendererEditor : UnityEditor.Editor
    {
        private SerializedProperty quality;
        private SerializedProperty useAdvancedShading;
        private SerializedProperty useSpecular;
        private SerializedProperty shininess;

        private SerializedProperty enableGPUCulling;
        private SerializedProperty enableLOD;
        private SerializedProperty enableDepthSorting;
        private SerializedProperty maxSplatsToRender;

        private SerializedProperty lod0Distance;
        private SerializedProperty lod1Distance;
        private SerializedProperty lod2Distance;
        private SerializedProperty lod3Distance;
        private SerializedProperty lod0Decimation;
        private SerializedProperty lod1Decimation;
        private SerializedProperty lod2Decimation;
        private SerializedProperty lod3Decimation;

        private SerializedProperty enableFrustumCulling;
        private SerializedProperty frustumPadding;
        private SerializedProperty nearClipDistance;
        private SerializedProperty farClipDistance;

        private SerializedProperty forceMobileMode;
        private SerializedProperty mobileMaxSplats;

        private bool showLODSettings = false;
        private bool showCullingSettings = false;
        private bool showMobileSettings = false;

        private void OnEnable()
        {
            quality = serializedObject.FindProperty("quality");
            useAdvancedShading = serializedObject.FindProperty("useAdvancedShading");
            useSpecular = serializedObject.FindProperty("useSpecular");
            shininess = serializedObject.FindProperty("shininess");

            enableGPUCulling = serializedObject.FindProperty("enableGPUCulling");
            enableLOD = serializedObject.FindProperty("enableLOD");
            enableDepthSorting = serializedObject.FindProperty("enableDepthSorting");
            maxSplatsToRender = serializedObject.FindProperty("maxSplatsToRender");

            lod0Distance = serializedObject.FindProperty("lod0Distance");
            lod1Distance = serializedObject.FindProperty("lod1Distance");
            lod2Distance = serializedObject.FindProperty("lod2Distance");
            lod3Distance = serializedObject.FindProperty("lod3Distance");
            lod0Decimation = serializedObject.FindProperty("lod0Decimation");
            lod1Decimation = serializedObject.FindProperty("lod1Decimation");
            lod2Decimation = serializedObject.FindProperty("lod2Decimation");
            lod3Decimation = serializedObject.FindProperty("lod3Decimation");

            enableFrustumCulling = serializedObject.FindProperty("enableFrustumCulling");
            frustumPadding = serializedObject.FindProperty("frustumPadding");
            nearClipDistance = serializedObject.FindProperty("nearClipDistance");
            farClipDistance = serializedObject.FindProperty("farClipDistance");

            forceMobileMode = serializedObject.FindProperty("forceMobileMode");
            mobileMaxSplats = serializedObject.FindProperty("mobileMaxSplats");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var renderer = (GaussianSplatAdvancedRenderer)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Advanced Gaussian Splat Renderer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("High-performance renderer with GPU culling, LOD, and mobile optimization.", MessageType.Info);
            EditorGUILayout.Space(10);

            // Rendering Quality
            EditorGUILayout.LabelField("Rendering Quality", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(quality);
            EditorGUILayout.PropertyField(useAdvancedShading);

            if (useAdvancedShading.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(useSpecular);
                if (useSpecular.boolValue)
                {
                    EditorGUILayout.PropertyField(shininess);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Performance
            EditorGUILayout.LabelField("Performance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableGPUCulling, new GUIContent("GPU Culling", "Use compute shaders for frustum and distance culling"));
            EditorGUILayout.PropertyField(enableLOD, new GUIContent("LOD System", "Reduce splat density based on distance"));
            EditorGUILayout.PropertyField(enableDepthSorting, new GUIContent("Depth Sorting", "Sort splats for proper alpha blending (expensive)"));
            EditorGUILayout.PropertyField(maxSplatsToRender, new GUIContent("Max Splats", "Maximum number of splats to render"));

            EditorGUILayout.Space(10);

            // LOD Settings
            showLODSettings = EditorGUILayout.Foldout(showLODSettings, "LOD Settings", true);
            if (showLODSettings && enableLOD.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Configure distance thresholds and decimation factors for each LOD level.", MessageType.None);

                EditorGUILayout.LabelField("Distances", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(lod0Distance, new GUIContent("LOD 0 Distance"));
                EditorGUILayout.PropertyField(lod1Distance, new GUIContent("LOD 1 Distance"));
                EditorGUILayout.PropertyField(lod2Distance, new GUIContent("LOD 2 Distance"));
                EditorGUILayout.PropertyField(lod3Distance, new GUIContent("LOD 3 Distance"));

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Decimation Factors (1.0 = all splats, 0.5 = half)", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(lod0Decimation, new GUIContent("LOD 0 Factor"));
                EditorGUILayout.PropertyField(lod1Decimation, new GUIContent("LOD 1 Factor"));
                EditorGUILayout.PropertyField(lod2Decimation, new GUIContent("LOD 2 Factor"));
                EditorGUILayout.PropertyField(lod3Decimation, new GUIContent("LOD 3 Factor"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Culling Settings
            showCullingSettings = EditorGUILayout.Foldout(showCullingSettings, "Culling Settings", true);
            if (showCullingSettings && enableGPUCulling.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(enableFrustumCulling);
                EditorGUILayout.PropertyField(frustumPadding, new GUIContent("Frustum Padding", "Extra padding for frustum bounds"));
                EditorGUILayout.PropertyField(nearClipDistance, new GUIContent("Near Clip"));
                EditorGUILayout.PropertyField(farClipDistance, new GUIContent("Far Clip"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Mobile Settings
            showMobileSettings = EditorGUILayout.Foldout(showMobileSettings, "Mobile Optimization", true);
            if (showMobileSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(forceMobileMode, new GUIContent("Force Mobile Mode", "Use mobile shaders even on desktop"));
                EditorGUILayout.PropertyField(mobileMaxSplats, new GUIContent("Mobile Max Splats", "Maximum splats on mobile devices"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            // Statistics (runtime only)
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Runtime Statistics", EditorStyles.boldLabel);

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Visible Splats", renderer.GetVisibleSplatCount());
                EditorGUILayout.FloatField("Culling Time (ms)", renderer.GetLastCullingTime() * 1000f);
                EditorGUILayout.Toggle("Using GPU Culling", renderer.IsUsingGPUCulling());
                EditorGUILayout.Toggle("Mobile Mode", renderer.IsMobileMode());
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(5);
                if (GUILayout.Button("Force Refresh"))
                {
                    SceneView.RepaintAll();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
