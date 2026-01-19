using System;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting
{
    [CreateAssetMenu(menuName = "UnitySplatter/Gaussian Splat Asset", fileName = "GaussianSplatAsset")]
    public sealed class GaussianSplatAsset : ScriptableObject
    {
        [SerializeField] private GaussianSplatFrameData frame = new GaussianSplatFrameData();

        public GaussianSplatFrameData Frame => frame;

        public bool IsValid(out string error)
        {
            if (frame == null)
            {
                error = "Gaussian splat asset frame is missing.";
                return false;
            }

            return frame.IsValid(out error);
        }

        public void SetFrame(GaussianSplatFrameData newFrame)
        {
            frame = newFrame ?? throw new ArgumentNullException(nameof(newFrame));
        }
    }
}
