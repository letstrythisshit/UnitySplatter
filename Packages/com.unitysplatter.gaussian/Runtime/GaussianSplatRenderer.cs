using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnitySplatter.Gaussian.Runtime
{
    [ExecuteAlways]
    public sealed class GaussianSplatRenderer : MonoBehaviour
    {
        [SerializeField] private GaussianSplatAsset asset;
        [SerializeField] private Material material;
        [SerializeField] private bool overrideBounds;
        [SerializeField] private Bounds bounds = new Bounds(Vector3.zero, new Vector3(10f, 10f, 10f));
        [SerializeField] private float opacityScale = 1f;
        [SerializeField] private float sizeScale = 1f;
        [SerializeField] private float minOpacity = 0.01f;
        [SerializeField] private bool enableFrustumCulling = true;
        [SerializeField] private bool enableSorting;
        [SerializeField] private bool enableShCoefficients = true;

        private ComputeBuffer pointBuffer;
        private int cachedCount;
        private static readonly int PointsId = Shader.PropertyToID("_Points");
        private static readonly int PointCountId = Shader.PropertyToID("_PointCount");
        private static readonly int OpacityScaleId = Shader.PropertyToID("_OpacityScale");
        private static readonly int SizeScaleId = Shader.PropertyToID("_SizeScale");
        private static readonly int MinOpacityId = Shader.PropertyToID("_MinOpacity");
        private static readonly int EnableShId = Shader.PropertyToID("_EnableSH");

        public GaussianSplatAsset Asset
        {
            get => asset;
            set
            {
                asset = value;
                Refresh();
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            ReleaseBuffers();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            ReleaseBuffers();
            if (asset == null || asset.Count == 0)
            {
                cachedCount = 0;
                return;
            }

            cachedCount = asset.Count;
            int stride = System.Runtime.InteropServices.Marshal.SizeOf<GaussianPoint>();
            pointBuffer = new ComputeBuffer(cachedCount, stride, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            var data = asset.Points.ToArray();
            pointBuffer.SetData(data);
        }

        private void ReleaseBuffers()
        {
            if (pointBuffer != null)
            {
                pointBuffer.Release();
                pointBuffer = null;
            }
        }

        private void OnRenderObject()
        {
            if (asset == null || material == null || pointBuffer == null)
            {
                return;
            }

            Camera currentCamera = Camera.current;
            if (currentCamera == null)
            {
                return;
            }

            material.SetBuffer(PointsId, pointBuffer);
            material.SetInt(PointCountId, cachedCount);
            material.SetFloat(OpacityScaleId, opacityScale);
            material.SetFloat(SizeScaleId, sizeScale);
            material.SetFloat(MinOpacityId, minOpacity);
            material.SetFloat(EnableShId, enableShCoefficients ? 1f : 0f);

            Bounds drawBounds = overrideBounds ? bounds : asset.Bounds;
            if (enableFrustumCulling && !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(currentCamera), drawBounds))
            {
                return;
            }

            Graphics.DrawProcedural(material, drawBounds, MeshTopology.Points, cachedCount, 1, null, null, ShadowCastingMode.Off, false, gameObject.layer);
        }
    }
}
