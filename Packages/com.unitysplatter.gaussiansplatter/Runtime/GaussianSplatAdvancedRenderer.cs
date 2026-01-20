using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

namespace UnitySplatter.GaussianSplatting
{
    /// <summary>
    /// Advanced high-performance renderer for Gaussian Splats with GPU culling, LOD, and sorting.
    /// Supports DirectX 11+, Vulkan, and OpenGL ES 3.1+ (Android).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(GaussianSplatRenderer))]
    public class GaussianSplatAdvancedRenderer : MonoBehaviour
    {
        [Header("Rendering Quality")]
        [SerializeField] private RenderingQuality quality = RenderingQuality.High;
        [SerializeField] private bool useAdvancedShading = true;
        [SerializeField] private bool useSpecular = false;
        [Range(1, 100)] [SerializeField] private float shininess = 20f;

        [Header("Performance")]
        [SerializeField] private bool enableGPUCulling = true;
        [SerializeField] private bool enableLOD = true;
        [SerializeField] private bool enableDepthSorting = false; // Expensive, enable only if needed
        [SerializeField] private int maxSplatsToRender = 1000000;

        [Header("LOD Settings")]
        [SerializeField] private float lod0Distance = 10f;
        [SerializeField] private float lod1Distance = 25f;
        [SerializeField] private float lod2Distance = 50f;
        [SerializeField] private float lod3Distance = 100f;
        [SerializeField] private float lod0Decimation = 1.0f; // 100%
        [SerializeField] private float lod1Decimation = 0.7f; // 70%
        [SerializeField] private float lod2Decimation = 0.4f; // 40%
        [SerializeField] private float lod3Decimation = 0.2f; // 20%

        [Header("Culling")]
        [SerializeField] private bool enableFrustumCulling = true;
        [SerializeField] private float frustumPadding = 0.1f;
        [SerializeField] private float nearClipDistance = 0.3f;
        [SerializeField] private float farClipDistance = 1000f;

        [Header("Mobile Optimization")]
        [SerializeField] private bool forceMobileMode = false;
        [SerializeField] private int mobileMaxSplats = 100000;

        // Compute shaders
        private ComputeShader cullingCompute;
        private ComputeShader sortingCompute;

        // Compute shader kernel indices
        private int cullKernel;
        private int sortKernel;
        private int depthKernel;

        // GPU buffers
        private ComputeBuffer visibilityMaskBuffer;
        private ComputeBuffer visibleIndicesBuffer;
        private ComputeBuffer visibleCountBuffer;
        private ComputeBuffer depthBuffer;
        private ComputeBuffer sortedIndicesBuffer;

        // Culled output buffers (for compact rendering)
        private ComputeBuffer culledPositionsBuffer;
        private ComputeBuffer culledScalesBuffer;
        private ComputeBuffer culledRotationsBuffer;
        private ComputeBuffer culledColorsBuffer;
        private ComputeBuffer culledOpacitiesBuffer;

        // Materials
        private Material advancedMaterial;
        private Material mobileMaterial;

        // Reference to base renderer
        private GaussianSplatRenderer baseRenderer;

        // State tracking
        private int lastSplatCount = -1;
        private bool buffersInitialized = false;
        private bool isMobileDevice;

        // Statistics
        private int visibleSplatCount = 0;
        private float lastCullingTime = 0f;

        public enum RenderingQuality
        {
            Low,
            Medium,
            High,
            Ultra
        }

        private void OnEnable()
        {
            // Check if we're on mobile
            isMobileDevice = Application.isMobilePlatform || forceMobileMode;

            // Get base renderer
            baseRenderer = GetComponent<GaussianSplatRenderer>();
            if (baseRenderer == null)
            {
                Debug.LogError("[GaussianSplatAdvancedRenderer] Base renderer not found!", this);
                enabled = false;
                return;
            }

            // Disable base renderer to avoid double rendering
            baseRenderer.enabled = false;

            // Load compute shaders
            LoadComputeShaders();

            // Load materials
            LoadMaterials();

            // Subscribe to rendering events
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            }
        }

        private void OnDisable()
        {
            // Cleanup
            ReleaseBuffers();

            // Unsubscribe from rendering events
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            }

            // Re-enable base renderer if it exists
            if (baseRenderer != null)
            {
                baseRenderer.enabled = true;
            }
        }

        private void OnDestroy()
        {
            ReleaseBuffers();

            if (advancedMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(advancedMaterial);
                else
                    DestroyImmediate(advancedMaterial);
            }

            if (mobileMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(mobileMaterial);
                else
                    DestroyImmediate(mobileMaterial);
            }
        }

        private void LoadComputeShaders()
        {
            try
            {
                cullingCompute = Resources.Load<ComputeShader>("GaussianSplatCulling");
                if (cullingCompute == null)
                {
                    Debug.LogWarning("[GaussianSplatAdvancedRenderer] Culling compute shader not found in Resources. GPU culling disabled.");
                    enableGPUCulling = false;
                }
                else
                {
                    cullKernel = cullingCompute.FindKernel("CombinedCullAndLOD");
                }

                sortingCompute = Resources.Load<ComputeShader>("GaussianSplatSorting");
                if (sortingCompute == null)
                {
                    Debug.LogWarning("[GaussianSplatAdvancedRenderer] Sorting compute shader not found. Depth sorting disabled.");
                    enableDepthSorting = false;
                }
                else
                {
                    depthKernel = sortingCompute.FindKernel("CalculateDepths");
                    sortKernel = sortingCompute.FindKernel("BitonicSort");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatAdvancedRenderer] Failed to load compute shaders: {e.Message}");
                enableGPUCulling = false;
                enableDepthSorting = false;
            }
        }

        private void LoadMaterials()
        {
            try
            {
                Shader advancedShader = Shader.Find("UnitySplatter/GaussianSplatAdvanced");
                if (advancedShader != null)
                {
                    advancedMaterial = new Material(advancedShader);
                    advancedMaterial.hideFlags = HideFlags.HideAndDontSave;
                    advancedMaterial.SetFloat("_OpacityMultiplier", 1.0f);
                    advancedMaterial.SetFloat("_ScaleMultiplier", 1.0f);
                    advancedMaterial.SetFloat("_AnisotropyStrength", 1.0f);
                    advancedMaterial.SetFloat("_Shininess", shininess);

                    if (useAdvancedShading)
                        advancedMaterial.EnableKeyword("USE_ADVANCED_SHADING");
                    if (useSpecular)
                        advancedMaterial.EnableKeyword("USE_SPECULAR");
                }
                else
                {
                    Debug.LogWarning("[GaussianSplatAdvancedRenderer] Advanced shader not found!");
                }

                Shader mobileShader = Shader.Find("UnitySplatter/GaussianSplatMobile");
                if (mobileShader != null)
                {
                    mobileMaterial = new Material(mobileShader);
                    mobileMaterial.hideFlags = HideFlags.HideAndDontSave;
                    mobileMaterial.SetFloat("_OpacityMultiplier", 1.0f);
                    mobileMaterial.SetFloat("_ScaleMultiplier", 1.0f);
                }
                else
                {
                    Debug.LogWarning("[GaussianSplatAdvancedRenderer] Mobile shader not found!");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatAdvancedRenderer] Failed to load materials: {e.Message}");
            }
        }

        private void InitializeBuffers(int splatCount)
        {
            if (splatCount <= 0)
                return;

            try
            {
                ReleaseBuffers();

                // Allocate culling buffers
                visibilityMaskBuffer = new ComputeBuffer(splatCount, sizeof(uint));
                visibleIndicesBuffer = new ComputeBuffer(splatCount, sizeof(uint));
                visibleCountBuffer = new ComputeBuffer(1, sizeof(uint));

                // Allocate depth and sorting buffers
                if (enableDepthSorting)
                {
                    depthBuffer = new ComputeBuffer(splatCount, sizeof(float));
                    sortedIndicesBuffer = new ComputeBuffer(splatCount, sizeof(uint));
                }

                // Allocate culled output buffers
                culledPositionsBuffer = new ComputeBuffer(splatCount, sizeof(float) * 3);
                culledScalesBuffer = new ComputeBuffer(splatCount, sizeof(float) * 3);
                culledRotationsBuffer = new ComputeBuffer(splatCount, sizeof(float) * 4);
                culledColorsBuffer = new ComputeBuffer(splatCount, sizeof(float) * 4);
                culledOpacitiesBuffer = new ComputeBuffer(splatCount, sizeof(float));

                buffersInitialized = true;
                lastSplatCount = splatCount;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatAdvancedRenderer] Failed to initialize buffers: {e.Message}");
                ReleaseBuffers();
            }
        }

        private void ReleaseBuffers()
        {
            visibilityMaskBuffer?.Release();
            visibleIndicesBuffer?.Release();
            visibleCountBuffer?.Release();
            depthBuffer?.Release();
            sortedIndicesBuffer?.Release();
            culledPositionsBuffer?.Release();
            culledScalesBuffer?.Release();
            culledRotationsBuffer?.Release();
            culledColorsBuffer?.Release();
            culledOpacitiesBuffer?.Release();

            visibilityMaskBuffer = null;
            visibleIndicesBuffer = null;
            visibleCountBuffer = null;
            depthBuffer = null;
            sortedIndicesBuffer = null;
            culledPositionsBuffer = null;
            culledScalesBuffer = null;
            culledRotationsBuffer = null;
            culledColorsBuffer = null;
            culledOpacitiesBuffer = null;

            buffersInitialized = false;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            RenderSplats(camera);
        }

        private void OnRenderObject()
        {
            // For built-in render pipeline
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                Camera camera = Camera.current;
                if (camera != null)
                {
                    RenderSplats(camera);
                }
            }
        }

        private void RenderSplats(Camera camera)
        {
            if (camera == null || baseRenderer == null)
                return;

            // Get splat data from base renderer
            GaussianSplatFrameData frameData = baseRenderer.GetCurrentFrameData();
            if (frameData == null || !frameData.IsValid(out _) || frameData.Count == 0)
                return;

            int splatCount = frameData.Count;

            // Apply mobile limits
            if (isMobileDevice && splatCount > mobileMaxSplats)
            {
                splatCount = mobileMaxSplats;
            }

            // Apply general limits
            splatCount = Mathf.Min(splatCount, maxSplatsToRender);

            // Initialize buffers if needed
            if (!buffersInitialized || lastSplatCount != splatCount)
            {
                InitializeBuffers(splatCount);
                if (!buffersInitialized)
                    return;
            }

            // Perform GPU culling if enabled
            int renderCount = splatCount;
            if (enableGPUCulling && cullingCompute != null)
            {
                renderCount = PerformGPUCulling(camera, frameData, splatCount);
            }

            if (renderCount <= 0)
                return;

            // Perform depth sorting if enabled
            if (enableDepthSorting && sortingCompute != null)
            {
                PerformDepthSorting(camera, frameData, renderCount);
            }

            // Render the splats
            RenderSplatsToCamera(camera, frameData, renderCount);
        }

        private int PerformGPUCulling(Camera camera, GaussianSplatFrameData frameData, int splatCount)
        {
            float startTime = Time.realtimeSinceStartup;

            try
            {
                // Get base renderer buffers
                var buffers = baseRenderer.GetGPUBuffers();
                if (buffers == null)
                    return splatCount;

                // Set input buffers
                cullingCompute.SetBuffer(cullKernel, "_InputPositions", buffers.positions);
                cullingCompute.SetBuffer(cullKernel, "_InputScales", buffers.scales);
                cullingCompute.SetBuffer(cullKernel, "_InputRotations", buffers.rotations);
                cullingCompute.SetBuffer(cullKernel, "_InputColors", buffers.colors);
                cullingCompute.SetBuffer(cullKernel, "_InputOpacities", buffers.opacities);

                // Set output buffers
                cullingCompute.SetBuffer(cullKernel, "_OutputPositions", culledPositionsBuffer);
                cullingCompute.SetBuffer(cullKernel, "_OutputScales", culledScalesBuffer);
                cullingCompute.SetBuffer(cullKernel, "_OutputRotations", culledRotationsBuffer);
                cullingCompute.SetBuffer(cullKernel, "_OutputColors", culledColorsBuffer);
                cullingCompute.SetBuffer(cullKernel, "_OutputOpacities", culledOpacitiesBuffer);

                // Set culling result buffers
                cullingCompute.SetBuffer(cullKernel, "_VisibilityMask", visibilityMaskBuffer);
                cullingCompute.SetBuffer(cullKernel, "_VisibleIndices", visibleIndicesBuffer);
                cullingCompute.SetBuffer(cullKernel, "_VisibleCount", visibleCountBuffer);

                // Set camera parameters
                Matrix4x4 vp = camera.projectionMatrix * camera.worldToCameraMatrix;
                cullingCompute.SetMatrix("_ViewProjectionMatrix", vp);
                cullingCompute.SetMatrix("_ViewMatrix", camera.worldToCameraMatrix);
                cullingCompute.SetVector("_CameraPosition", camera.transform.position);
                cullingCompute.SetVector("_CameraForward", camera.transform.forward);
                cullingCompute.SetFloat("_NearClip", nearClipDistance);
                cullingCompute.SetFloat("_FarClip", farClipDistance);
                cullingCompute.SetFloat("_FrustumPadding", frustumPadding);

                // Set LOD parameters
                cullingCompute.SetVector("_LODDistances", new Vector4(lod0Distance, lod1Distance, lod2Distance, lod3Distance));
                cullingCompute.SetVector("_LODDecimation", new Vector4(lod0Decimation, lod1Decimation, lod2Decimation, lod3Decimation));

                // Set splat count
                cullingCompute.SetInt("_SplatCount", splatCount);

                // Clear visible count
                uint[] zeroCount = new uint[] { 0 };
                visibleCountBuffer.SetData(zeroCount);

                // Dispatch compute shader
                int threadGroups = Mathf.CeilToInt(splatCount / 256.0f);
                cullingCompute.Dispatch(cullKernel, threadGroups, 1, 1);

                // Read back visible count
                uint[] countData = new uint[1];
                visibleCountBuffer.GetData(countData);
                visibleSplatCount = (int)countData[0];

                lastCullingTime = Time.realtimeSinceStartup - startTime;

                return visibleSplatCount;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GaussianSplatAdvancedRenderer] GPU culling failed: {e.Message}");
                return splatCount;
            }
        }

        private void PerformDepthSorting(Camera camera, GaussianSplatFrameData frameData, int splatCount)
        {
            // TODO: Implement bitonic sort
            // This is complex and requires multiple dispatch calls
            // For now, we'll skip it and rely on depth testing
        }

        private void RenderSplatsToCamera(Camera camera, GaussianSplatFrameData frameData, int renderCount)
        {
            // Choose material based on device and quality
            Material renderMaterial = (isMobileDevice && mobileMaterial != null) ? mobileMaterial : advancedMaterial;

            if (renderMaterial == null)
            {
                // Fallback to base renderer
                baseRenderer.enabled = true;
                return;
            }

            // Set buffers to material
            if (enableGPUCulling && visibleSplatCount > 0)
            {
                // Use culled buffers
                renderMaterial.SetBuffer("_Positions", culledPositionsBuffer);
                renderMaterial.SetBuffer("_Scales", culledScalesBuffer);
                renderMaterial.SetBuffer("_Rotations", culledRotationsBuffer);
                renderMaterial.SetBuffer("_Colors", culledColorsBuffer);
                renderMaterial.SetBuffer("_Opacities", culledOpacitiesBuffer);

                renderCount = visibleSplatCount;
            }
            else
            {
                // Use original buffers from base renderer
                var buffers = baseRenderer.GetGPUBuffers();
                if (buffers == null)
                    return;

                renderMaterial.SetBuffer("_Positions", buffers.positions);
                renderMaterial.SetBuffer("_Scales", buffers.scales);
                renderMaterial.SetBuffer("_Rotations", buffers.rotations);
                renderMaterial.SetBuffer("_Colors", buffers.colors);
                renderMaterial.SetBuffer("_Opacities", buffers.opacities);
            }

            // Set render state
            renderMaterial.SetPass(0);

            // Draw procedural
            Graphics.DrawProceduralNow(MeshTopology.Points, renderCount);
        }

        // Public API for getting statistics
        public int GetVisibleSplatCount() => visibleSplatCount;
        public float GetLastCullingTime() => lastCullingTime;
        public bool IsUsingGPUCulling() => enableGPUCulling && cullingCompute != null;
        public bool IsMobileMode() => isMobileDevice;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Update material properties when changed in inspector
            if (advancedMaterial != null)
            {
                advancedMaterial.SetFloat("_Shininess", shininess);

                if (useAdvancedShading)
                    advancedMaterial.EnableKeyword("USE_ADVANCED_SHADING");
                else
                    advancedMaterial.DisableKeyword("USE_ADVANCED_SHADING");

                if (useSpecular)
                    advancedMaterial.EnableKeyword("USE_SPECULAR");
                else
                    advancedMaterial.DisableKeyword("USE_SPECULAR");
            }
        }
#endif
    }

    // Extension method to get GPU buffers from base renderer
    public static class GaussianSplatRendererExtensions
    {
        public static (ComputeBuffer positions, ComputeBuffer scales, ComputeBuffer rotations, ComputeBuffer colors, ComputeBuffer opacities)? GetGPUBuffers(this GaussianSplatRenderer renderer)
        {
            // Use reflection to access private buffers
            var type = typeof(GaussianSplatRenderer);
            var positionsField = type.GetField("positionsBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var scalesField = type.GetField("scalesBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rotationsField = type.GetField("rotationsBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var colorsField = type.GetField("colorsBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var opacitiesField = type.GetField("opacitiesBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (positionsField == null || scalesField == null || rotationsField == null || colorsField == null || opacitiesField == null)
                return null;

            var positions = positionsField.GetValue(renderer) as ComputeBuffer;
            var scales = scalesField.GetValue(renderer) as ComputeBuffer;
            var rotations = rotationsField.GetValue(renderer) as ComputeBuffer;
            var colors = colorsField.GetValue(renderer) as ComputeBuffer;
            var opacities = opacitiesField.GetValue(renderer) as ComputeBuffer;

            if (positions == null || scales == null || rotations == null || colors == null || opacities == null)
                return null;

            return (positions, scales, rotations, colors, opacities);
        }

        public static GaussianSplatFrameData GetCurrentFrameData(this GaussianSplatRenderer renderer)
        {
            if (renderer == null || renderer.Asset == null)
                return null;

            return renderer.Asset.Frame;
        }
    }
}
