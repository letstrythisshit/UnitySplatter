using System.Collections.Generic;
using UnityEngine;

namespace UnitySplatter.Gaussian.Playback
{
    [CreateAssetMenu(menuName = "UnitySplatter/Gaussian Splat Sequence", fileName = "GaussianSplatSequence")]
    public class GaussianSplatSequenceAsset : ScriptableObject
    {
        [SerializeField] private List<GaussianSplatAsset> frames = new();
        [SerializeField] private float frameRate = 30f;
        [SerializeField] private string sourceDirectory;

        public IReadOnlyList<GaussianSplatAsset> Frames => frames;
        public float FrameRate => frameRate;
        public string SourceDirectory => sourceDirectory;

        public void Initialize(List<GaussianSplatAsset> assets, float fps, string directory)
        {
            frames = assets ?? new List<GaussianSplatAsset>();
            frameRate = Mathf.Max(1f, fps);
            sourceDirectory = directory;
        }
    }
}
