using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnitySplatter.Gaussian.Utilities;

namespace UnitySplatter.Gaussian.Rendering
{
    [DisallowMultipleComponent]
    public sealed class GaussianSplatRenderer : MonoBehaviour
    {
        [SerializeField] private GaussianSplatAsset asset;
        [SerializeField] private Material material;
        [SerializeField, Range(0.01f, 10f)] private float globalScale = 1f;
        [SerializeField, Range(0f, 1f)] private float opacityMultiplier = 1f;
        [SerializeField] private bool frustumCulling = true;

        private ComputeBuffer splatBuffer;
        private int splatCount;
        private Bounds bounds;

        private static readonly int SplatBufferId = Shader.PropertyToID("_SplatBuffer");
        private static readonly int GlobalScaleId = Shader.PropertyToID("_GlobalScale");
        private static readonly int OpacityMultiplierId = Shader.PropertyToID("_OpacityMultiplier");

        public GaussianSplatAsset Asset
        {
            get => asset;
            set
            {
                asset = value;
                UploadAsset();
            }
        }

        private void OnEnable()
        {
            if (!PlatformSupport.IsGraphicsApiSupported())
            {
                Debug.LogWarning("GaussianSplatRenderer: Unsupported graphics API. DirectX or Vulkan required.");
            }
            UploadAsset();
        }

        private void OnDisable()
        {
            ReleaseBuffers();
        }

        private void Update()
        {
            if (asset == null || material == null || splatBuffer == null)
            {
                return;
            }

            material.SetFloat(GlobalScaleId, globalScale);
            material.SetFloat(OpacityMultiplierId, opacityMultiplier);
            material.SetBuffer(SplatBufferId, splatBuffer);

            var renderBounds = frustumCulling ? bounds : new Bounds(transform.position, Vector3.one * 100000f);
            Graphics.DrawProcedural(material, renderBounds, MeshTopology.Points, splatCount, 1, null, null,
                ShadowCastingMode.Off, false, gameObject.layer);
        }

        public void UploadAsset()
        {
            ReleaseBuffers();
            if (asset == null)
            {
                return;
            }

            var splats = asset.Splats;
            if (splats == null || splats.Count == 0)
            {
                return;
            }

            splatCount = splats.Count;
            bounds = asset.Bounds;
            var gpuData = new SplatGpuData[splatCount];
            for (var i = 0; i < splatCount; i++)
            {
                var splat = splats[i];
                gpuData[i] = new SplatGpuData
                {
                    Position = splat.Position,
                    Scale = splat.Scale,
                    Rotation = new Vector4(splat.Rotation.x, splat.Rotation.y, splat.Rotation.z, splat.Rotation.w),
                    Color = new Vector4(splat.Color.r, splat.Color.g, splat.Color.b, splat.Opacity)
                };
            }

            splatBuffer = new ComputeBuffer(splatCount, SplatGpuData.Stride, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            splatBuffer.SetData(gpuData);
        }

        private void ReleaseBuffers()
        {
            if (splatBuffer != null)
            {
                splatBuffer.Release();
                splatBuffer = null;
            }
        }

        [Serializable]
        private struct SplatGpuData
        {
            public Vector3 Position;
            public Vector3 Scale;
            public Vector4 Rotation;
            public Vector4 Color;

            public const int Stride = sizeof(float) * (3 + 3 + 4 + 4);
        }
    }
}
