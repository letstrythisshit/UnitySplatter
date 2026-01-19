using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnitySplatter.GaussianSplatting
{
    [CreateAssetMenu(menuName = "UnitySplatter/Gaussian Splat Sequence", fileName = "GaussianSplatSequence")]
    public sealed class GaussianSplatSequenceAsset : ScriptableObject
    {
        [SerializeField] private List<GaussianSplatFrameData> frames = new();
        [SerializeField] private float framesPerSecond = 30f;
        [SerializeField] private bool loop = true;

        public IReadOnlyList<GaussianSplatFrameData> Frames => frames;
        public float FramesPerSecond => framesPerSecond;
        public bool Loop => loop;

        public void SetFrames(List<GaussianSplatFrameData> newFrames, float fps, bool shouldLoop)
        {
            frames = newFrames ?? throw new ArgumentNullException(nameof(newFrames));
            framesPerSecond = Mathf.Max(1f, fps);
            loop = shouldLoop;
        }

        public bool IsValid(out string error)
        {
            error = string.Empty;
            if (frames == null || frames.Count == 0)
            {
                error = "Gaussian splat sequence has no frames.";
                return false;
            }

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] == null)
                {
                    error = $"Gaussian splat sequence frame {i} is missing.";
                    return false;
                }

                if (!frames[i].IsValid(out error))
                {
                    error = $"Gaussian splat sequence frame {i} is invalid: {error}";
                    return false;
                }
            }

            if (framesPerSecond <= 0f)
            {
                error = "Gaussian splat sequence FPS must be positive.";
                return false;
            }

            return true;
        }
    }
}
