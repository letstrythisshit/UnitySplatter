using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnitySplatter.GaussianSplatting
{
    [DisallowMultipleComponent]
    public sealed class GaussianSplatRenderer : MonoBehaviour
    {
        [SerializeField] private GaussianSplatAsset asset;
        [SerializeField] private Material materialOverride;
        [SerializeField] private bool enableFrustumCulling = true;
        [SerializeField] private float opacityMultiplier = 1f;
        [SerializeField] private bool useSRPRendering = true;

        private GraphicsBuffer positionBuffer;
        private GraphicsBuffer scaleBuffer;
        private GraphicsBuffer rotationBuffer;
        private GraphicsBuffer colorBuffer;
        private GraphicsBuffer opacityBuffer;
        private Material runtimeMaterial;
        private int splatCount;
        private Bounds bounds;

        private static readonly int PositionBufferId = Shader.PropertyToID("_SplatPositions");
        private static readonly int ScaleBufferId = Shader.PropertyToID("_SplatScales");
        private static readonly int RotationBufferId = Shader.PropertyToID("_SplatRotations");
        private static readonly int ColorBufferId = Shader.PropertyToID("_SplatColors");
        private static readonly int OpacityBufferId = Shader.PropertyToID("_SplatOpacities");
        private static readonly int OpacityMultiplierId = Shader.PropertyToID("_OpacityMultiplier");

        public GaussianSplatAsset Asset
        {
            get => asset;
            set
            {
                asset = value;
                Reload();
            }
        }

        public bool EnableFrustumCulling
        {
            get => enableFrustumCulling;
            set => enableFrustumCulling = value;
        }

        public float OpacityMultiplier
        {
            get => opacityMultiplier;
            set => opacityMultiplier = Mathf.Max(0f, value);
        }

        private void OnEnable()
        {
            Reload();
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            ReleaseBuffers();
        }

        private void OnValidate()
        {
            opacityMultiplier = Mathf.Max(0f, opacityMultiplier);
            if (!Application.isPlaying)
            {
                Reload();
            }
        }

        public void Reload()
        {
            ReleaseBuffers();

            if (asset == null || !asset.IsValid(out _))
            {
                splatCount = 0;
                return;
            }

            GaussianSplatFrameData frame = asset.Frame;
            splatCount = frame.Count;
            if (splatCount == 0)
            {
                return;
            }

            positionBuffer = CreateBuffer(frame.Positions, sizeof(float) * 3);
            scaleBuffer = CreateBuffer(frame.Scales, sizeof(float) * 3);
            rotationBuffer = CreateBuffer(frame.Rotations, sizeof(float) * 4);
            colorBuffer = CreateBuffer(frame.Colors, sizeof(float) * 4);
            opacityBuffer = CreateBuffer(frame.Opacities, sizeof(float));

            bounds = GaussianSplatOptimizer.CalculateBounds(frame.Positions, frame.Scales);
            SetupMaterial();
        }

        private void SetupMaterial()
        {
            Shader shader = Shader.Find("UnitySplatter/GaussianSplat");
            if (shader == null)
            {
                Debug.LogWarning("GaussianSplat shader not found. Assign a material override.", this);
                return;
            }

            runtimeMaterial = materialOverride != null ? new Material(materialOverride) : new Material(shader);
            runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
            runtimeMaterial.SetBuffer(PositionBufferId, positionBuffer);
            runtimeMaterial.SetBuffer(ScaleBufferId, scaleBuffer);
            runtimeMaterial.SetBuffer(RotationBufferId, rotationBuffer);
            runtimeMaterial.SetBuffer(ColorBufferId, colorBuffer);
            runtimeMaterial.SetBuffer(OpacityBufferId, opacityBuffer);
        }

        private GraphicsBuffer CreateBuffer<T>(T[] data, int stride) where T : struct
        {
            GraphicsBuffer buffer = new(GraphicsBuffer.Target.Structured, data.Length, stride);
            buffer.SetData(data);
            return buffer;
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!useSRPRendering)
            {
                return;
            }

            DrawSplats(camera);
        }

        private void OnRenderObject()
        {
            if (useSRPRendering)
            {
                return;
            }

            DrawSplats(Camera.current);
        }

        private void DrawSplats(Camera camera)
        {
            if (runtimeMaterial == null || splatCount == 0 || camera == null)
            {
                return;
            }

            runtimeMaterial.SetFloat(OpacityMultiplierId, opacityMultiplier);

            if (enableFrustumCulling && !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bounds))
            {
                return;
            }

            runtimeMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Points, splatCount);
        }

        private void ReleaseBuffers()
        {
            positionBuffer?.Release();
            scaleBuffer?.Release();
            rotationBuffer?.Release();
            colorBuffer?.Release();
            opacityBuffer?.Release();
            positionBuffer = null;
            scaleBuffer = null;
            rotationBuffer = null;
            colorBuffer = null;
            opacityBuffer = null;

            if (runtimeMaterial != null)
            {
                DestroyImmediate(runtimeMaterial);
                runtimeMaterial = null;
            }
        }
    }
}
