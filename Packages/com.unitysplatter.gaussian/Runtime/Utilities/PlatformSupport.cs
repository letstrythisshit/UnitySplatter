using UnityEngine;

namespace UnitySplatter.Gaussian.Utilities
{
    public static class PlatformSupport
    {
        public static bool IsGraphicsApiSupported()
        {
            switch (SystemInfo.graphicsDeviceType)
            {
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D11:
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D12:
                case UnityEngine.Rendering.GraphicsDeviceType.Vulkan:
                    return true;
                default:
                    return false;
            }
        }
    }
}
