using System;
using UnityEngine;

namespace UnitySplatter.Gaussian.Runtime
{
    [CreateAssetMenu(fileName = "GaussianSplatSequenceAsset", menuName = "UnitySplatter/Gaussian Splat Sequence Asset", order = 2)]
    public sealed class GaussianSplatSequenceAsset : ScriptableObject
    {
        [SerializeField] private GaussianSplatAsset[] frames = Array.Empty<GaussianSplatAsset>();
        [SerializeField] private float framesPerSecond = 30f;
        [SerializeField] private bool loop = true;

        public ReadOnlySpan<GaussianSplatAsset> Frames => frames;
        public float FramesPerSecond => Mathf.Max(1f, framesPerSecond);
        public bool Loop => loop;
        public int FrameCount => frames?.Length ?? 0;

        public GaussianSplatAsset GetFrame(int index)
        {
            if (frames == null || frames.Length == 0)
            {
                return null;
            }

            int clamped = Mathf.Clamp(index, 0, frames.Length - 1);
            return frames[clamped];
        }

        public int GetFrameIndex(float time)
        {
            if (frames == null || frames.Length == 0)
            {
                return 0;
            }

            float frame = time * FramesPerSecond;
            int index = Mathf.FloorToInt(frame);
            if (loop)
            {
                index %= frames.Length;
            }
            else
            {
                index = Mathf.Clamp(index, 0, frames.Length - 1);
            }

            return index;
        }
    }
}
